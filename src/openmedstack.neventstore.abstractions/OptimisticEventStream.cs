using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenMedStack.NEventStore.Abstractions;

/// <summary>
/// Defines the default implementation of the <see cref="IEventStream"/> interface.
/// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="OptimisticEventStream"/> class.
    /// </summary>
    /// <param name="bucketId">The id of the tenant.</param>
    /// <param name="streamId">The id of the stream.</param>
    /// <param name="logger">The <see cref="ILogger{T}"/> to use.</param>
    private OptimisticEventStream(
        string bucketId,
        string streamId,
        ILogger<OptimisticEventStream> logger)
    {
        BucketId = bucketId;
        StreamId = streamId;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new empty event stream.
    /// </summary>
    /// <param name="bucketId">The bucket id.</param>
    /// <param name="streamId">The stream id.</param>
    /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> to use.</param>
    /// <returns>An empty event stream at version 0.</returns>
    public static OptimisticEventStream Create(
        string bucketId,
        string streamId,
        ILogger<OptimisticEventStream>? logger = null) =>
        new(bucketId, streamId, logger ?? NullLogger<OptimisticEventStream>.Instance);

    /// <summary>
    /// Loads an event stream from the event storage.
    /// </summary>
    /// <param name="bucketId">The bucket id.</param>
    /// <param name="streamId">The stream id.</param>
    /// <param name="persistence">The event storage.</param>
    /// <param name="minRevision">The minimum revision to include.</param>
    /// <param name="maxRevision">The maximum revision to include.</param>
    /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> to use.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use for the async operation.</param>
    /// <returns>An event stream with the commits within the requested range.</returns>
    /// <exception cref="StreamNotFoundException">Thrown if the stream does not exist in the storage.</exception>
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

    /// <summary>
    /// Creates an event stream from the snapshot and the event storage.
    /// </summary>
    /// <param name="snapshot">The snapshot to build the stream from.</param>
    /// <param name="persistence">The event storage.</param>
    /// <param name="maxRevision">The maximum revision to include.</param>
    /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> to use.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use for the async operation.</param>
    /// <returns>An event stream based on the loaded snapshot.</returns>
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

    /// <summary>
    /// Gets the bucket id.
    /// </summary>
    public string BucketId { get; }

    /// <summary>
    /// Gets the stream id.
    /// </summary>
    public string StreamId { get; }

    /// <summary>
    /// Gets the stream revision.
    /// </summary>
    public int StreamRevision { get; private set; }

    /// <summary>
    /// Gets the commit sequence.
    /// </summary>
    public int CommitSequence { get; private set; }

    /// <summary>
    /// Gets the committed events.
    /// </summary>
    public IReadOnlyCollection<EventMessage> CommittedEvents
    {
        get { return _committed.ToImmutableArray(); }
    }

    /// <summary>
    /// Gets the committed headers.
    /// </summary>
    public IReadOnlyDictionary<string, object> CommittedHeaders
    {
        get { return _committedHeaders; }
    }

    /// <summary>
    /// Gets the uncommitted events.
    /// </summary>
    public IReadOnlyCollection<EventMessage> UncommittedEvents
    {
        get { return _events.ToImmutableArray(); }
    }

    /// <summary>
    /// Gets the uncommitted headers.
    /// </summary>
    public IReadOnlyDictionary<string, object> UncommittedHeaders
    {
        get { return _uncommittedHeaders; }
    }

    /// <summary>
    /// Adds the event messages provided to the session to be tracked.
    /// </summary>
    /// <param name="uncommittedEvent">The <see cref="EventMessage"/> to add.</param>
    public void Add(EventMessage uncommittedEvent)
    {
        _logger.LogTrace(Resources.AppendingUncommittedToStream, uncommittedEvent.Body.GetType(), StreamId);
        _events.Add(uncommittedEvent);
        StreamRevision++;
    }

    /// <summary>
    /// Adds the key value pair to the uncommitted headers.
    /// </summary>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value.</param>
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

    /// <summary>
    /// Updates the event stream with the events contained in the event storage.
    /// </summary>
    /// <param name="commitEvents">The reference to the <see cref="ICommitEvents">event storage</see>.</param>
    /// <param name="cancellationToken"></param>
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

    /// <summary>
    /// Removes all uncommitted events and headers from the stream.
    /// </summary>
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
