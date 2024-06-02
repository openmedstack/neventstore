using Microsoft.AspNetCore.Mvc;
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
        return await _persistence.Commit(attempt, cancellationToken).ConfigureAwait(false);
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
