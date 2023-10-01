using Grpc.Net.Client;

namespace OpenMedStack.NEventStore.GrpcClient;

using Microsoft.Extensions.Logging;
using OpenMedStack.NEventStore.Abstractions;

/// <summary>
/// Defines the GRPC based implementation of <see cref="IStoreEvents"/>.
/// </summary>
public class GrpcEventStoreClient : IStoreEvents
{
    private readonly ILoggerFactory _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcEventStoreClient"/> class.
    /// </summary>
    /// <param name="endpoint">The server endpoint.</param>
    /// <param name="serializer">The event serializer.</param>
    /// <param name="logger">The <see cref="ILogger{TCategoryName}"/>.</param>
    /// <param name="channelOptions">The optional channel options.</param>
    public GrpcEventStoreClient(
        Uri endpoint,
        ISerialize serializer,
        ILoggerFactory logger,
        GrpcChannelOptions? channelOptions = null)
    {
        Advanced = new GrpcEventStorePersistence(endpoint, serializer, channelOptions);
        _logger = logger;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Advanced.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public IPersistStreams Advanced { get; }

    /// <inheritdoc />
    public async Task<IEventStream> CreateStream(string bucketId, string streamId, CancellationToken cancellationToken)
    {
        return await OptimisticEventStream.Create(bucketId, streamId, Advanced, 0, int.MaxValue,
            _logger.CreateLogger<OptimisticEventStream>(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEventStream> OpenStream(
        string bucketId,
        string streamId,
        int minRevision,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        return await OptimisticEventStream.Create(bucketId, streamId, Advanced, minRevision, maxRevision,
            _logger.CreateLogger<OptimisticEventStream>(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEventStream> OpenStream(ISnapshot snapshot, int maxRevision, CancellationToken cancellationToken)
    {
        return await OptimisticEventStream
            .Create(snapshot, Advanced, maxRevision, _logger.CreateLogger<OptimisticEventStream>(), cancellationToken)
            .ConfigureAwait(false);
    }
}
