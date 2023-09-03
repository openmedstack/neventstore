using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace OpenMedStack.NEventStore.Abstractions;

[SuppressMessage(
    "Microsoft.Naming",
    "CA1711:IdentifiersShouldNotHaveIncorrectSuffix",
    Justification = "This behaves like a stream--not a .NET 'Stream' object, but a stream nonetheless.")]
public sealed class OptimisticEventStream : IEventStream
{
    private readonly ILogger<OptimisticEventStream> _logger;
    private readonly ICollection<EventMessage> _committed = new LinkedList<EventMessage>();
    private readonly ICollection<EventMessage> _events = new LinkedList<EventMessage>();
    private readonly ICollection<Guid> _identifiers = new HashSet<Guid>();
    private readonly ICommitEvents _persistence;

    private bool _disposed;
//    private readonly ImmutableArray<EventMessage> _immutableCollection;
//    private readonly ImmutableArray<EventMessage> _uncommittedEvents;

    private OptimisticEventStream(
        string bucketId,
        string streamId,
        ICommitEvents persistence,
        ILogger<OptimisticEventStream> logger)
    {
        BucketId = bucketId;
        StreamId = streamId;
        _persistence = persistence;
        _logger = logger;
    }

    public static Task<OptimisticEventStream> Create(
        string bucketId,
        string streamId,
        ICommitEvents persistence,
        ILogger<OptimisticEventStream> logger) =>
        Task.FromResult(new OptimisticEventStream(bucketId, streamId, persistence, logger));

    public static async Task<OptimisticEventStream> Create(
        string bucketId,
        string streamId,
        ICommitEvents persistence,
        int minRevision,
        int maxRevision,
        ILogger<OptimisticEventStream> logger,
        CancellationToken cancellationToken = default)
    {
        var instance = await Create(bucketId, streamId, persistence, logger).ConfigureAwait(false);
        var commits = persistence.GetFrom(bucketId, streamId, minRevision, maxRevision, cancellationToken);
        await instance.PopulateStream(minRevision, maxRevision, commits, cancellationToken).ConfigureAwait(false);

        if (minRevision > 0 && instance._committed.Count == 0)
        {
            throw new StreamNotFoundException(
                string.Format(Resources.StreamNotFoundException, streamId, instance.BucketId));
        }

        return instance;
    }

    public static async Task<OptimisticEventStream> Create(
        ISnapshot snapshot,
        ICommitEvents persistence,
        int maxRevision,
        ILogger<OptimisticEventStream> logger,
        CancellationToken cancellationToken)
    {
        var instance = await Create(snapshot.BucketId, snapshot.StreamId, persistence, logger).ConfigureAwait(false);
        var commits = persistence.GetFrom(
            snapshot.BucketId,
            snapshot.StreamId,
            snapshot.StreamRevision,
            maxRevision,
            cancellationToken);
        await instance.PopulateStream(snapshot.StreamRevision + 1, maxRevision, commits, cancellationToken)
            .ConfigureAwait(false);
        instance.StreamRevision = snapshot.StreamRevision + instance._committed.Count;

        return instance;
    }

    public string BucketId { get; }
    public string StreamId { get; }
    public int StreamRevision { get; private set; }
    public int CommitSequence { get; private set; }

    public IReadOnlyCollection<EventMessage> CommittedEvents
    {
        get { return ImmutableArray.CreateRange(_committed); }
    }

    public IDictionary<string, object> CommittedHeaders { get; } = new Dictionary<string, object>();

    public IReadOnlyCollection<EventMessage> UncommittedEvents
    {
        get { return ImmutableArray.CreateRange(_events); }
    }

    public IDictionary<string, object> UncommittedHeaders { get; } = new Dictionary<string, object>();

    public void Add(EventMessage uncommittedEvent)
    {
        if (uncommittedEvent == null)
        {
            throw new ArgumentNullException(nameof(uncommittedEvent));
        }

        if (uncommittedEvent.Body == null)
        {
            throw new Exception(nameof(uncommittedEvent.Body));
        }

        _logger.LogTrace(Resources.AppendingUncommittedToStream, uncommittedEvent.Body.GetType(), StreamId);
        _events.Add(uncommittedEvent);
    }

    public async Task CommitChanges(Guid commitId, CancellationToken cancellationToken)
    {
        _logger.LogTrace(Resources.AttemptingToCommitChanges, StreamId);

        if (_identifiers.Contains(commitId))
        {
            throw new DuplicateCommitException(string.Format(Resources.DuplicateCommitIdException, commitId));
        }

        if (!HasChanges())
        {
            return;
        }

        try
        {
            await PersistChanges(commitId, cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyException cex)
        {
            _logger.LogDebug(
                Resources.UnderlyingStreamHasChanged,
                StreamId,
                cex.Message); //not useful to log info because the exception will be thrown
            var commits = _persistence.GetFrom(
                BucketId,
                StreamId,
                StreamRevision + 1,
                int.MaxValue,
                cancellationToken);
            await PopulateStream(StreamRevision + 1, int.MaxValue, commits, cancellationToken)
                .ConfigureAwait(false);

            throw;
        }
    }

    public Task ClearChanges()
    {
        _logger.LogTrace(Resources.ClearingUncommittedChanges, StreamId);
        _events.Clear();
        UncommittedHeaders.Clear();

        return Task.CompletedTask;
    }

    private async Task PopulateStream(
        int minRevision,
        int maxRevision,
        IAsyncEnumerable<ICommit> commits,
        CancellationToken cancellationToken)
    {
        await foreach (var commit in commits.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _logger.LogTrace(Resources.AddingCommitsToStream, commit.CommitId, commit.Events.Count, StreamId);
            _identifiers.Add(commit.CommitId);

            CommitSequence = commit.CommitSequence;
            var currentRevision = commit.StreamRevision - commit.Events.Count + 1;
            if (currentRevision > maxRevision)
            {
                return;
            }

            CopyToCommittedHeaders(commit);
            CopyToEvents(minRevision, maxRevision, currentRevision, commit);
        }
    }

    private void CopyToCommittedHeaders(ICommit commit)
    {
        foreach (var key in commit.Headers.Keys)
        {
            CommittedHeaders[key] = commit.Headers[key];
        }
    }

    private void CopyToEvents(int minRevision, int maxRevision, int currentRevision, ICommit commit)
    {
        foreach (var @event in commit.Events)
        {
            if (currentRevision > maxRevision)
            {
                _logger.LogDebug(Resources.IgnoringBeyondRevision, commit.CommitId, StreamId, maxRevision);
                break;
            }

            if (currentRevision++ < minRevision)
            {
                _logger.LogDebug(Resources.IgnoringBeforeRevision, commit.CommitId, StreamId, maxRevision);
                continue;
            }

            _committed.Add(@event);
            StreamRevision = currentRevision - 1;
        }
    }

    private bool HasChanges()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(Resources.AlreadyDisposed);
        }

        if (_events.Count > 0)
        {
            return true;
        }

        _logger.LogInformation(Resources.NoChangesToCommit, StreamId);
        return false;
    }

    private async Task PersistChanges(Guid commitId, CancellationToken cancellationToken)
    {
        var attempt = BuildCommitAttempt(commitId);

        _logger.LogDebug(Resources.PersistingCommit, commitId, StreamId, attempt.Events.Count);
        var commit = await _persistence.Commit(attempt).ConfigureAwait(false);

        await PopulateStream(
                StreamRevision + 1,
                attempt.StreamRevision,
                (commit == null ? Array.Empty<ICommit>() : new[] { commit }).ToAsyncEnumerable(cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
        await ClearChanges().ConfigureAwait(false);
    }

    private CommitAttempt BuildCommitAttempt(Guid commitId)
    {
        _logger.LogTrace(Resources.BuildingCommitAttempt, commitId, StreamId);
        return new CommitAttempt(
            BucketId,
            StreamId,
            StreamRevision + _events.Count,
            commitId,
            CommitSequence + 1,
            SystemTime.UtcNow,
            UncommittedHeaders.ToDictionary(x => x.Key, x => x.Value),
            _events.ToList());
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
