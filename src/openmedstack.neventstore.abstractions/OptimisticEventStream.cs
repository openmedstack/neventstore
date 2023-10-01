using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenMedStack.NEventStore.Abstractions;

[SuppressMessage(
    "Microsoft.Naming",
    "CA1711:IdentifiersShouldNotHaveIncorrectSuffix",
    Justification = "This behaves like a stream--not a .NET 'Stream' object, but a stream nonetheless.")]
public sealed class OptimisticEventStream : IEventStream
{
    private readonly ILogger<OptimisticEventStream> _logger;
    private readonly List<EventMessage> _committed = new();
    private readonly List<EventMessage> _events = new();
    private readonly ICollection<Guid> _identifiers = new HashSet<Guid>();

//    private readonly ImmutableArray<EventMessage> _immutableCollection;
//    private readonly ImmutableArray<EventMessage> _uncommittedEvents;

    private OptimisticEventStream(
        string bucketId,
        string streamId,
        ILogger<OptimisticEventStream> logger)
    {
        BucketId = bucketId;
        StreamId = streamId;
        _logger = logger;
    }

    public static OptimisticEventStream Create(
        string bucketId,
        string streamId,
        ILogger<OptimisticEventStream>? logger = null) =>
        new(bucketId, streamId, logger ?? NullLogger<OptimisticEventStream>.Instance);

    public static async Task<OptimisticEventStream> Create(
        string bucketId,
        string streamId,
        ICommitEvents persistence,
        int minRevision,
        int maxRevision,
        ILogger<OptimisticEventStream> logger,
        CancellationToken cancellationToken = default)
    {
        var instance = Create(bucketId, streamId, logger);
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
        var instance = Create(snapshot.BucketId, snapshot.StreamId, logger);
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

    internal static async Task<OptimisticEventStream> Create(
        string bucketId,
        string streamId,
        IAsyncEnumerable<ICommit> commits,
        int minRevision = 0,
        int maxRevision = int.MaxValue,
        int streamRevision = 0,
        int commitSequence = 0,
        ILogger<OptimisticEventStream>? logger = null)
    {
        var instance = Create(bucketId, streamId, logger ?? NullLogger<OptimisticEventStream>.Instance);
        await instance.PopulateStream(minRevision, maxRevision, commits, CancellationToken.None).ConfigureAwait(false);

        if (minRevision > 0 && instance._committed.Count == 0)
        {
            throw new StreamNotFoundException(
                string.Format(Resources.StreamNotFoundException, streamId, instance.BucketId));
        }

        if (streamRevision > 0)
        {
            instance.StreamRevision = streamRevision;
        }

        if (commitSequence > 0)
        {
            instance.CommitSequence = commitSequence;
        }

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
        StreamRevision++;
    }

    public void SetPersisted(int commitSequence)
    {
        _committed.AddRange(_events);
        foreach (var header in UncommittedHeaders)
        {
            CommittedHeaders[header.Key] = header.Value;
        }

        CommitSequence = commitSequence;

        ClearChanges();
    }

    public async Task Update(ICommitEvents commitEvents, CancellationToken cancellationToken = default)
    {
        var revision = StreamRevision - UncommittedEvents.Count;
        var commits = commitEvents.GetFrom(BucketId, StreamId, revision + 1, int.MaxValue,
            cancellationToken);
        await PopulateStream(revision + 1, int.MaxValue, commits, cancellationToken).ConfigureAwait(false);
    }

    internal void ClearChanges()
    {
        _logger.LogTrace(Resources.ClearingUncommittedChanges, StreamId);
        _events.Clear();
        UncommittedHeaders.Clear();
    }

    private async Task PopulateStream(
        int minRevision,
        int maxRevision,
        IAsyncEnumerable<ICommit> commits,
        CancellationToken cancellationToken)
    {
        await foreach (var commit in commits.ConfigureAwait(false).WithCancellation(cancellationToken)
            .ConfigureAwait(false))
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
}
