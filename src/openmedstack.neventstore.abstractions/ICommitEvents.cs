namespace OpenMedStack.NEventStore.Abstractions;

/// <summary>
///     Indicates the ability to commit events and access events to and from a given stream.
/// </summary>
/// <remarks>
///     Instances of this class must be designed to be multi-thread safe such that they can be shared between threads.
/// </remarks>
public interface ICommitEvents : IDisposable
{
    /// <summary>
    ///     Gets the corresponding commits from the stream indicated starting at the revision specified until the
    ///     end of the stream sorted in ascending order--from oldest to newest.
    /// </summary>
    /// <param name="tenantId">The value which uniquely identifies bucket the stream belongs to.</param>
    /// <param name="streamId">The stream from which the events will be read.</param>
    /// <param name="minRevision">The minimum revision of the stream to be read.</param>
    /// <param name="maxRevision">The maximum revision of the stream to be read.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> for the async operation</param>
    /// <returns>A series of committed events from the stream specified sorted in ascending order.</returns>
    /// <exception cref="StorageException" />
    /// <exception cref="StorageUnavailableException" />
    IAsyncEnumerable<ICommit> Get(
        string tenantId,
        string streamId,
        int minRevision = 0,
        int maxRevision = int.MaxValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Writes the to-be-committed events stream provided to the underlying persistence mechanism.
    /// </summary>
    /// <param name="commitAttempt">The series of events and associated metadata to be committed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> for the async operation</param>
    /// <exception cref="ConcurrencyException" />
    /// <exception cref="StorageException" />
    /// <exception cref="StorageUnavailableException" />
    Task<ICommit?> Commit(
        CommitAttempt commitAttempt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the commit dispatcher interface.
/// </summary>
public interface ICommitDispatcher : IDisposable
{
    /// <summary>
    /// Dispatches the commit to the appropriate handlers.
    /// </summary>
    /// <param name="commit">The <see cref="ICommit"/> to dispatch.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> for the async operation.</param>
    /// <returns></returns>
    Task<HandlingResult> Dispatch(ICommit commit, CancellationToken cancellationToken);
}
