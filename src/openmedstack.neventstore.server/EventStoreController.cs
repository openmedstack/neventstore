using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Server;

[Route("")]
public class EventStoreController : Controller
{
    private readonly IPersistStreams _persistence;

    public EventStoreController(IPersistStreams persistence)
    {
        _persistence = persistence;
    }

    [HttpGet("/commits/{bucketId}/{streamId}/{minRevision:int?}/{maxRevision:int?}")]
    public IAsyncEnumerable<ICommit> GetFrom(
        string bucketId,
        string streamId,
        int? minRevision,
        int? maxRevision,
        CancellationToken cancellationToken) => _persistence.GetFrom(
        bucketId,
        streamId,
        minRevision ?? 0,
        maxRevision ?? int.MaxValue,
        cancellationToken);

    [HttpGet(template: "/commits/{bucketId}/{start}")]
    public IAsyncEnumerable<ICommit> GetFrom(
        string bucketId,
        DateTimeOffset start,
        CancellationToken cancellationToken = default)
    {
        return _persistence.GetFrom(bucketId: bucketId, start: start, cancellationToken: cancellationToken);
    }

    [HttpPost("/commits")]
    public async Task<ICommit?> Commit([FromBody] CommitAttempt attempt, CancellationToken cancellationToken)
    {
        var stream = await OptimisticEventStream.Create(attempt.BucketId, attempt.StreamId, _persistence, 0,
            attempt.StreamRevision,
            NullLogger<OptimisticEventStream>.Instance, cancellationToken).ConfigureAwait(false);
        foreach (var eventMessage in attempt.Events)
        {
            stream.Add(eventMessage);
        }

        foreach (var header in attempt.Headers)
        {
            stream.UncommittedHeaders[header.Key] = header.Value;
        }

        return await _persistence.Commit(stream, attempt.CommitId, cancellationToken).ConfigureAwait(false);
    }

    [HttpGet("/commits/{bucketId}/{checkpointToken}")]
    public IAsyncEnumerable<ICommit> GetFrom(string bucketId, long checkpointToken, CancellationToken cancellationToken)
    {
        return _persistence.GetFrom(bucketId, checkpointToken, cancellationToken);
    }

    [HttpGet("/snapshots/{bucketId}/{streamId}/{maxRevision:int}")]
    public Task<ISnapshot?> GetSnapshot(
        string bucketId,
        string streamId,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        return _persistence.GetSnapshot(bucketId, streamId, maxRevision, cancellationToken);
    }

    [HttpPost("/snapshots")]
    public Task<bool> AddSnapshot([FromBody] ISnapshot snapshot)
    {
        return _persistence.AddSnapshot(snapshot);
    }

    [HttpGet("/streams/{bucketId}/{maxThreshold:int}")]
    public IAsyncEnumerable<IStreamHead> GetStreamsToSnapshot(
        string bucketId,
        int maxThreshold,
        CancellationToken cancellationToken)
    {
        return _persistence.GetStreamsToSnapshot(bucketId, maxThreshold, cancellationToken);
    }

    [HttpGet("/commits/{bucketId}/{start}/{end}")]
    public IAsyncEnumerable<ICommit> GetFromTo(
        string bucketId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        return _persistence.GetFromTo(bucketId, start, end, cancellationToken);
    }

    [HttpDelete("streams/{bucketId}")]
    public Task<bool> Purge(string bucketId)
    {
        return _persistence.Purge(bucketId);
    }

    [HttpDelete("streams/{bucketId}/{streamId}")]
    public Task DeleteStream(string bucketId, string streamId)
    {
        return _persistence.DeleteStream(bucketId, streamId);
    }
}
