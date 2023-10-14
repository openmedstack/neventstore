using System.Collections.ObjectModel;

namespace OpenMedStack.NEventStore.Abstractions;

public class CommitAttempt
{
    /// <summary>
    ///     Initializes a new instance of the Commit class.
    /// </summary>
    /// <param name="bucketId">The value which identifies bucket to which the the stream and the the commit belongs</param>
    /// <param name="streamId">The value which uniquely identifies the stream in a bucket to which the commit belongs.</param>
    /// <param name="streamRevision">The value which indicates the revision of the most recent event in the stream to which this commit applies.</param>
    /// <param name="commitId">The value which uniquely identifies the commit within the stream.</param>
    /// <param name="commitSequence">The value which indicates the sequence (or position) in the stream to which this commit applies.</param>
    /// <param name="commitStamp">The point in time at which the commit was persisted.</param>
    /// <param name="headers">The metadata which provides additional, unstructured information about this commit.</param>
    /// <param name="events">The collection of event messages to be committed as a single unit.</param>
    public CommitAttempt(
        string bucketId,
        string streamId,
        int streamRevision,
        Guid commitId,
        int commitSequence,
        DateTimeOffset commitStamp,
        IDictionary<string, object>? headers,
        IList<EventMessage> events)
    {
        Guard.NotNullOrWhiteSpace(nameof(bucketId), bucketId);
        Guard.NotNullOrWhiteSpace(nameof(streamId), streamId);
        Guard.NotLessThanOrEqualTo(nameof(streamRevision), streamRevision, 0);
        Guard.NotDefault(nameof(commitId), commitId);
        Guard.NotLessThanOrEqualTo(nameof(commitSequence), commitSequence, 0);
        Guard.NotLessThan(nameof(commitSequence), streamRevision, 0);
        Guard.NotEmpty(nameof(events), events);

        BucketId = bucketId;
        StreamId = streamId;
        StreamRevision = streamRevision;
        CommitId = commitId;
        CommitSequence = commitSequence;
        CommitStamp = commitStamp;
        Headers = headers ?? new Dictionary<string, object>();
        Events = new ReadOnlyCollection<EventMessage>(events);
    }

    public static CommitAttempt FromStream(IEventStream eventStream, Guid commitId)
    {
        return new CommitAttempt(
            eventStream.BucketId,
            eventStream.StreamId,
            eventStream.StreamRevision,
            commitId,
            eventStream.CommitSequence + 1,
            DateTimeOffset.UtcNow,
            eventStream.UncommittedHeaders.ToDictionary(),
            eventStream.UncommittedEvents.ToList());
    }

    internal static CommitAttempt FromCommit(ICommit commit)
    {
        return new CommitAttempt(
            commit.BucketId,
            commit.StreamId,
            commit.StreamRevision,
            commit.CommitId,
            commit.CommitSequence,
            commit.CommitStamp,
            commit.Headers,
            commit.Events.ToList());
    }

    /// <summary>
    ///     Gets the value which identifies bucket to which the the stream and the the commit belongs.
    /// </summary>
    public string BucketId { get; }

    /// <summary>
    ///     Gets the value which uniquely identifies the stream to which the commit belongs.
    /// </summary>
    public string StreamId { get; }

    /// <summary>
    ///     Gets the value which indicates the revision of the most recent event in the stream to which this commit applies.
    /// </summary>
    public int StreamRevision { get; }

    /// <summary>
    ///     Gets the value which uniquely identifies the commit within the stream.
    /// </summary>
    public Guid CommitId { get; }

    /// <summary>
    ///     Gets the value which indicates the sequence (or position) in the stream to which this commit applies.
    /// </summary>
    public int CommitSequence { get; }

    /// <summary>
    ///     Gets the point in time at which the commit was persisted.
    /// </summary>
    public DateTimeOffset CommitStamp { get; }

    /// <summary>
    ///     Gets the metadata which provides additional, unstructured information about this commit.
    /// </summary>
    public IDictionary<string, object> Headers { get; }

    /// <summary>
    ///     Gets the collection of event messages to be committed as a single unit.
    /// </summary>
    public ICollection<EventMessage> Events { get; }
}
