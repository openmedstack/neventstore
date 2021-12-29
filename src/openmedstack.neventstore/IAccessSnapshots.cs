using System.Collections.Generic;
using System.Threading.Tasks;
using OpenMedStack.NEventStore.Persistence;

namespace OpenMedStack.NEventStore
{
    using System.Threading;

    /// <summary>
    ///     Indicates the ability to get or retrieve a snapshot for a given stream.
    /// </summary>
    /// <remarks>
    ///     Instances of this class must be designed to be multi-thread safe such that they can be shared between threads.
    /// </remarks>
    public interface IAccessSnapshots
    {
        /// <summary>
        ///     Gets the most recent snapshot which was taken on or before the revision indicated.
        /// </summary>
        /// <param name="bucketId">The value which uniquely identifies bucket the stream belongs to.</param>
        /// <param name="streamId">The stream to be searched for a snapshot.</param>
        /// <param name="maxRevision">The maximum revision possible for the desired snapshot.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>If found, it returns the snapshot; otherwise null is returned.</returns>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        Task<ISnapshot?> GetSnapshot(string bucketId, string streamId, int maxRevision, CancellationToken cancellationToken);

        /// <summary>
        ///     Adds the snapshot provided to the stream indicated.
        /// </summary>
        /// <param name="snapshot">The snapshot to save.</param>
        /// <returns>If the snapshot was added, returns true; otherwise false.</returns>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        Task<bool> AddSnapshot(ISnapshot snapshot);

        /// <summary>
        ///     Gets identifiers for all streams whose head and last snapshot revisions differ by at least the threshold specified.
        /// </summary>
        /// <param name="bucketId">The value which uniquely identifies bucket the stream belongs to.</param>
        /// <param name="maxThreshold">The maximum difference between the head and most recent snapshot revisions.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The streams for which the head and snapshot revisions differ by at least the threshold specified.</returns>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        IAsyncEnumerable<IStreamHead> GetStreamsToSnapshot(string bucketId, int maxThreshold, CancellationToken cancellationToken);
    }
}