namespace OpenMedStack.NEventStore.Abstractions;

/// <summary>
///     Indicates the ability to track a series of events and commit them to durable storage.
/// </summary>
/// <remarks>
///     Instances of this class are single threaded and should not be shared between threads.
/// </remarks>
public interface IEventStream
{
    /// <summary>
    ///     Gets the value which identifies bucket to which the the stream belongs.
    /// </summary>
    string BucketId { get; }

    /// <summary>
    ///     Gets the value which uniquely identifies the stream to which the stream belongs.
    /// </summary>
    string StreamId { get; }

    /// <summary>
    ///     Gets the value which indicates the most recent committed revision of event stream.
    /// </summary>
    int StreamRevision { get; }

    /// <summary>
    ///     Gets the value which indicates the most recent committed sequence identifier of the event stream.
    /// </summary>
    int CommitSequence { get; }

    /// <summary>
    ///     Gets the collection of events which have been successfully persisted to durable storage.
    /// </summary>
    IReadOnlyCollection<EventMessage> CommittedEvents { get; }

    /// <summary>
    ///     Gets the collection of committed headers associated with the stream.
    /// </summary>
    IReadOnlyDictionary<string, object> CommittedHeaders { get; }

    /// <summary>
    ///     Gets the collection of yet-to-be-committed events that have not yet been persisted to durable storage.
    /// </summary>
    IReadOnlyCollection<EventMessage> UncommittedEvents { get; }

    /// <summary>
    ///     Gets the collection of yet-to-be-committed headers associated with the uncommitted events.
    /// </summary>
    IReadOnlyDictionary<string, object> UncommittedHeaders { get; }

    /// <summary>
    ///     Adds the event messages provided to the session to be tracked.
    /// </summary>
    /// <param name="uncommittedEvent">The event to be tracked.</param>
    void Add(EventMessage uncommittedEvent);

    /// <summary>
    /// Adds the key value pair to the uncommitted headers.
    /// </summary>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value.</param>
    void Add(string key, object value);

    /// <summary>
    ///  Moves the uncommitted events to the committed collection and clears the uncommitted collection.
    /// </summary>
    void SetPersisted(int commitSequence);

    /// <summary>
    /// Updates the event stream with the events contained in the event storage.
    /// </summary>
    /// <param name="commitEvents">The event storage.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> for the async operation</param>
    /// <returns>The update operation as a <see cref="Task"/>.</returns>
    Task Update(ICommitEvents commitEvents, CancellationToken cancellationToken = default);
}
