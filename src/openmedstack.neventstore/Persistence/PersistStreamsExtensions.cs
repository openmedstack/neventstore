using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Persistence;

using System.Runtime.CompilerServices;
using System.Threading;

public static class PersistStreamsExtensions
{
    /// <summary>
    ///     Gets all commits after from start checkpoint.
    /// </summary>
    /// <param name="managePersistence">The IPersistStreams instance.</param>
    /// <param name="bucketId">The bucket to load</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> for the async operation.</param>
    public static async IAsyncEnumerable<ICommit> GetFromStart(
        this IManagePersistence managePersistence,
        string bucketId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (managePersistence == null)
        {
            throw new ArgumentException("persistStreams is null");
        }

        await Task.Yield();
        await foreach (var item in managePersistence.GetFrom(bucketId, 0, cancellationToken).ConfigureAwait(false))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            yield return item;
        }
    }
}
