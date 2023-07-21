using System;
using System.Threading.Tasks;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Persistence;

namespace OpenMedStack.NEventStore;

using System.Threading;

/// <summary>
///     Indicates the ability to store and retreive a stream of events.
/// </summary>
/// <remarks>
///     Instances of this class must be designed to be multi-thread safe such that they can be shared between threads.
/// </remarks>
public interface IStoreEvents : IDisposable
{
    /// <summary>
    ///     Gets a reference to the underlying persistence engine which allows direct access to persistence operations.
    /// </summary>
    IPersistStreams Advanced { get; }

    /// <summary>
    ///     Creates a new stream.
    /// </summary>
    /// <param name="bucketId">The value which uniquely identifies bucket the stream belongs to.</param>
    /// <param name="streamId">The value which uniquely identifies the stream within the bucket to be created.</param>
    /// <returns>An empty stream.</returns>
    Task<IEventStream> CreateStream(string bucketId, string streamId);

    /// <summary>
    ///     Reads the stream indicated from the minimum revision specified up to the maximum revision specified or creates
    ///     an empty stream if no commits are found and a minimum revision of zero is provided.
    /// </summary>
    /// <param name="bucketId">The value which uniquely identifies bucket the stream belongs to.</param>
    /// <param name="streamId">The value which uniquely identifies the stream in the bucket from which the events will be read.</param>
    /// <param name="minRevision">The minimum revision of the stream to be read.</param>
    /// <param name="maxRevision">The maximum revision of the stream to be read.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A series of committed events represented as a stream.</returns>
    /// <exception cref="StorageException" />
    /// <exception cref="StorageUnavailableException" />
    /// <exception cref="StreamNotFoundException" />
    Task<IEventStream> OpenStream(string bucketId, string streamId, int minRevision, int maxRevision, CancellationToken cancellationToken);

    /// <summary>
    ///     Reads the stream indicated from the point of the snapshot forward until the maximum revision specified.
    /// </summary>
    /// <param name="snapshot">The snapshot of the stream to be read.</param>
    /// <param name="maxRevision">The maximum revision of the stream to be read.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A series of committed events represented as a stream.</returns>
    /// <exception cref="StorageException" />
    /// <exception cref="StorageUnavailableException" />
    Task<IEventStream> OpenStream(ISnapshot snapshot, int maxRevision, CancellationToken cancellationToken);
}