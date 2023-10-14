using Microsoft.Extensions.Logging;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.HttpClient;

/// <summary>
/// Defines the HTTP based implementation of <see cref="IStoreEvents"/>.
/// </summary>
public class HttpEventStoreClient
{
    private readonly ICommitEvents _persistence;
    private readonly ILoggerFactory _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpEventStoreClient"/> class.
    /// </summary>
    /// <param name="handler">The <see cref="HttpMessageHandler"/> to use.</param>
    /// <param name="baseUri">The base <see cref="Uri"/>.</param>
    /// <param name="serializer">The object serializer</param>
    /// <param name="logger">The <see cref="ILogger{TCategoryName}"/>.</param>
    public HttpEventStoreClient(
        HttpMessageHandler handler,
        Uri baseUri,
        ISerialize serializer,
        ILoggerFactory logger)
    {
        _logger = logger;
        _persistence = new HttpEventStorePersistence(new System.Net.Http.HttpClient(handler, false)
            { BaseAddress = baseUri }, serializer);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _persistence.Dispose();
    }

    /// <inheritdoc />
    public async Task<IEventStream> CreateStream(string bucketId, string streamId, CancellationToken cancellationToken)
    {
        return await OptimisticEventStream.Create(bucketId, streamId, _persistence, 0, int.MaxValue,
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
        return await OptimisticEventStream.Create(bucketId, streamId, _persistence, minRevision, maxRevision,
            _logger.CreateLogger<OptimisticEventStream>(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEventStream> OpenStream(ISnapshot snapshot, int maxRevision, CancellationToken cancellationToken)
    {
        return await OptimisticEventStream.Create(snapshot, _persistence, maxRevision,
            _logger.CreateLogger<OptimisticEventStream>(), cancellationToken);
    }
}
