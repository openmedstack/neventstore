using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.Persistence;

using System.Runtime.CompilerServices;
using System.Threading;

public static class PersistStreamsExtensions
{
    /// <summary>
    ///     Gets all commits on or after from the specified starting time from the default bucket.
    /// </summary>
    /// <param name="managePersistence">The IPersistStreams instance.</param>
    /// <param name="start">The point in time at which to start.</param>
    /// <returns>All commits that have occurred on or after the specified starting time.</returns>
    /// <exception cref="StorageException" />
    /// <exception cref="StorageUnavailableException" />
    public static IAsyncEnumerable<ICommit> GetFrom(this IManagePersistence managePersistence, DateTimeOffset start)
    {
        if (managePersistence == null)
        {
            throw new ArgumentException("persistStreams is null");
        }

        return managePersistence.GetFrom(Bucket.Default, start);
    }

    /// <summary>
    ///     Gets all commits after from start checkpoint.
    /// </summary>
    /// <param name="managePersistence">The IPersistStreams instance.</param>
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
