using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Server;

[Route("")]
public class EventStoreController : Controller
{
    private readonly ICommitEvents _persistence;
    private readonly IAccessSnapshots _snapshots;

    public EventStoreController(ICommitEvents persistence, IAccessSnapshots snapshots)
    {
        _persistence = persistence;
        _snapshots = snapshots;
    }

    [HttpGet("/commits/{bucketId}/{streamId}/{minRevision:int?}/{maxRevision:int?}")]
    public IAsyncEnumerable<ICommit> GetFrom(
        string bucketId,
        string streamId,
        int? minRevision,
        int? maxRevision,
        CancellationToken cancellationToken) => _persistence.Get(
        bucketId,
        streamId,
        minRevision ?? 0,
        maxRevision ?? int.MaxValue,
        cancellationToken);

    [HttpPost("/commit")]
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
            stream.Add(header.Key, header.Value);
        }

        return await _persistence.Commit(stream, attempt.CommitId, cancellationToken).ConfigureAwait(false);
    }

    [HttpGet("/snapshots/{bucketId}/{streamId}/{maxRevision:int}")]
    public Task<ISnapshot?> GetSnapshot(
        string bucketId,
        string streamId,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        return _snapshots.GetSnapshot(bucketId, streamId, maxRevision, cancellationToken);
    }

    [HttpPost("/snapshots")]
    public Task<bool> AddSnapshot([FromBody] ISnapshot snapshot)
    {
        return _snapshots.AddSnapshot(snapshot);
    }
}
