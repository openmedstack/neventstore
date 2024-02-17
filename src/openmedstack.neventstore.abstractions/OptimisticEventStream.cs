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
    private readonly Dictionary<string, object> _uncommittedHeaders = new();
    private readonly Dictionary<string, object> _committedHeaders = new();

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
        int minRevision = 0,
        int maxRevision = int.MaxValue,
        ILogger<OptimisticEventStream>? logger = null,
        CancellationToken cancellationToken = default)
    {
        var instance = Create(bucketId, streamId, logger ?? NullLogger<OptimisticEventStream>.Instance);
        var commits = persistence.Get(bucketId, streamId, minRevision, maxRevision, cancellationToken);
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
        var commits = persistence.Get(
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
        get { return _committed.ToList(); }
    }

    public IReadOnlyDictionary<string, object> CommittedHeaders
    {
        get { return _committedHeaders; }
    }

    public IReadOnlyCollection<EventMessage> UncommittedEvents
    {
        get { return _events.ToList(); }
    }

    public IReadOnlyDictionary<string, object> UncommittedHeaders
    {
        get { return _uncommittedHeaders; }
    }

    public void Add(EventMessage uncommittedEvent)
    {
        _logger.LogTrace(Resources.AppendingUncommittedToStream, uncommittedEvent.Body.GetType(), StreamId);
        _events.Add(uncommittedEvent);
        StreamRevision++;
    }

    public void Add(string key, object value)
    {
        _uncommittedHeaders[key] = value;
    }

    public void SetPersisted(int commitSequence)
    {
        _committed.AddRange(_events);
        foreach (var header in UncommittedHeaders)
        {
            _committedHeaders[header.Key] = header.Value;
        }

        CommitSequence = commitSequence;

        ClearChanges();
    }

    public async Task Update(ICommitEvents commitEvents, CancellationToken cancellationToken = default)
    {
        var revision = StreamRevision - UncommittedEvents.Count;
        var commits = commitEvents.Get(BucketId, StreamId, revision + 1, int.MaxValue,
            cancellationToken);
        await PopulateStream(revision + 1, int.MaxValue, commits, cancellationToken).ConfigureAwait(false);
        var toBeCommitted = UncommittedEvents.ToArray();
        _events.Clear();
        foreach (var @event in toBeCommitted)
        {
            Add(@event);
        }
    }

    internal void ClearChanges()
    {
        _logger.LogTrace(Resources.ClearingUncommittedChanges, StreamId);
        _events.Clear();
        _uncommittedHeaders.Clear();
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
            _committedHeaders[key] = commit.Headers[key];
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
