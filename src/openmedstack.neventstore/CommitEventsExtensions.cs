using System;
using System.Collections.Generic;
using OpenMedStack.NEventStore.Persistence;

namespace OpenMedStack.NEventStore
{
    using System.Threading;

    public static class CommitEventsExtensions
    {
        /// <summary>
        ///     Gets the corresponding commits from the stream indicated starting at the revision specified until the
        ///     end of the stream sorted in ascending order--from oldest to newest from the default bucket.
        /// </summary>
        /// <param name="commitEvents">The <see cref="ICommitEvents"/> instance.</param>
        /// <param name="streamId">The stream from which the events will be read.</param>
        /// <param name="minRevision">The minimum revision of the stream to be read.</param>
        /// <param name="maxRevision">The maximum revision of the stream to be read.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A series of committed events from the stream specified sorted in ascending order.</returns>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        public static IAsyncEnumerable<ICommit> GetFrom(this ICommitEvents commitEvents, string streamId, int minRevision, int maxRevision, CancellationToken cancellationToken)
        {
            if (commitEvents == null)
            {
                throw new ArgumentException("commitEvents is null");
            }
            return commitEvents.GetFrom(Bucket.Default, streamId, minRevision, maxRevision, cancellationToken);
        }
    }
}