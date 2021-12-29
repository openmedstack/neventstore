namespace OpenMedStack.NEventStore
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenMedStack.NEventStore.Persistence;

    public static class AccessSnapshotsExtensions
    {
        /// <summary>
        ///     Gets the most recent snapshot which was taken on or before the revision indicated from the default bucket.
        /// </summary>
        /// <param name="accessSnapshots">The <see cref="IAccessSnapshots"/> instance.</param>
        /// <param name="streamId">The stream to be searched for a snapshot.</param>
        /// <param name="maxRevision">The maximum revision possible for the desired snapshot.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>If found, it returns the snapshot; otherwise null is returned.</returns>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        public static Task<ISnapshot?> GetSnapshot(this IAccessSnapshots accessSnapshots, Guid streamId, int maxRevision, CancellationToken cancellationToken) => GetSnapshot(accessSnapshots, streamId.ToString(), maxRevision, cancellationToken);

        /// <summary>
        ///     Gets the most recent snapshot which was taken on or before the revision indicated from the default bucket.
        /// </summary>
        /// <param name="accessSnapshots">The <see cref="IAccessSnapshots"/> instance.</param>
        /// <param name="streamId">The stream to be searched for a snapshot.</param>
        /// <param name="maxRevision">The maximum revision possible for the desired snapshot.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>If found, it returns the snapshot; otherwise null is returned.</returns>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        public static Task<ISnapshot?> GetSnapshot(
            this IAccessSnapshots accessSnapshots,
            string streamId,
            int maxRevision,
            CancellationToken cancellationToken) =>
            accessSnapshots.GetSnapshot(Bucket.Default, streamId, maxRevision, cancellationToken);

        /// <summary>
        ///     Gets the most recent snapshot which was taken on or before the revision indicated.
        /// </summary>
        /// <param name="accessSnapshots">The <see cref="IAccessSnapshots"/> instance.</param>
        /// <param name="bucketId">The value which uniquely identifies bucket the stream belongs to.</param>
        /// <param name="streamId">The stream to be searched for a snapshot.</param>
        /// <param name="maxRevision">The maximum revision possible for the desired snapshot.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>If found, it returns the snapshot; otherwise null is returned.</returns>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        public static Task<ISnapshot?> GetSnapshot(this IAccessSnapshots accessSnapshots, string bucketId, Guid streamId, int maxRevision, CancellationToken cancellationToken)
        {
            if (accessSnapshots == null)
            {
                throw new ArgumentException("accessSnapshots is null");
            }
            return accessSnapshots.GetSnapshot(bucketId, streamId.ToString(), maxRevision, cancellationToken);
        }

        /// <summary>
        ///     Gets identifiers for all streams whose head and last snapshot revisions differ by at least the threshold specified for the default bucket.
        /// </summary>
        /// <param name="accessSnapshots">The <see cref="IAccessSnapshots"/> instance.</param>
        /// <param name="maxThreshold">The maximum difference between the head and most recent snapshot revisions.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The streams for which the head and snapshot revisions differ by at least the threshold specified.</returns>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        public static IAsyncEnumerable<IStreamHead> GetStreamsToSnapshot(this IAccessSnapshots accessSnapshots, int maxThreshold, CancellationToken cancellationToken)
        {
            if (accessSnapshots == null)
            {
                throw new ArgumentException("accessSnapshots is null");
            }
            return accessSnapshots.GetStreamsToSnapshot(Bucket.Default, maxThreshold, cancellationToken);
        }
    }
}