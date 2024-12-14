namespace OpenMedStack.NEventStore.Abstractions;

using System.Threading;

/// <summary>
///     Indicates the ability to adapt the underlying persistence infrastructure to behave like a stream of events.
/// </summary>
/// <remarks>
///     Instances of this class must be designed to be multi-thread safe such that they can be shared between threads.
/// </remarks>
public interface IManagePersistence
{
    /// <summary>
    ///     Initializes and prepares the storage for use, if not already performed.
    /// </summary>
    /// <exception cref="StorageException" />
    /// <exception cref="StorageUnavailableException" />
    Task Initialize();

    /// <summary>
    ///     Gets all commits after from the specified checkpoint. Use null to get from the beginning.
    /// </summary>
    /// <param name="bucketId">The value which uniquely identifies bucket the stream belongs to.</param>
    /// <param name="checkpointToken">The checkpoint token.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> for the async operation</param>
    /// <returns>An enumerable of Commits.</returns>
    IAsyncEnumerable<ICommit> GetFrom(string bucketId, long checkpointToken, CancellationToken cancellationToken);

    /// <summary>
    ///     Gets identifiers for all streams whose head and last snapshot revisions differ by at least the threshold specified.
    /// </summary>
    /// <param name="bucketId">The value which uniquely identifies bucket the stream belongs to.</param>
    /// <param name="maxThreshold">The maximum difference between the head and most recent snapshot revisions.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> for the async operation</param>
    /// <returns>The streams for which the head and snapshot revisions differ by at least the threshold specified.</returns>
    /// <exception cref="StorageException" />
    /// <exception cref="StorageUnavailableException" />
    IAsyncEnumerable<IStreamHead> GetStreamsToSnapshot(string bucketId, int maxThreshold, CancellationToken cancellationToken);

    /// <summary>
    ///     Completely DESTROYS the contents of ANY and ALL streams that have been successfully persisted
    ///     in the specified bucket.  Use with caution.
    /// </summary>
    Task<bool> Purge(string bucketId);

    /// <summary>
    ///     Completely DESTROYS the contents and schema (if applicable) containing ANY and ALL streams that have been
    ///     successfully persisted
    ///     in the specified bucket.  Use with caution.
    /// </summary>
    Task<bool> Drop();

//    /// <summary>
//    /// Deletes a stream.
//    /// </summary>
//    /// <param name="bucketId">The bucket Id from which the stream is to be deleted.</param>
//    /// <param name="streamId">The stream Id of the stream that is to be deleted.</param>
//    Task<bool> DeleteStream(string bucketId, string streamId);
}
