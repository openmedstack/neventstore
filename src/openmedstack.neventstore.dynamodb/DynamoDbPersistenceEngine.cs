using System.Runtime.CompilerServices;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.DynamoDb;

public class DynamoDbPersistenceEngine : ICommitEvents, IAccessSnapshots
{
    private readonly IDynamoDBContext _context;
    private readonly ISerialize _serializer;

    public DynamoDbPersistenceEngine(IDynamoDBContext context, ISerialize serializer)
    {
        _context = context;
        _serializer = serializer;
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    public async IAsyncEnumerable<ICommit> Get(
        string bucketId,
        string streamId,
        int minRevision,
        int maxRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _context.ScanAsync<DynamoDbCommit>(
            [
                new ScanCondition(nameof(DynamoDbCommit.BucketId), ScanOperator.Equal, bucketId),
                new ScanCondition(nameof(DynamoDbCommit.StreamId), ScanOperator.Equal, streamId),
                new ScanCondition(nameof(DynamoDbCommit.StreamRevision), ScanOperator.GreaterThanOrEqual, minRevision),
                new ScanCondition(nameof(DynamoDbCommit.StreamRevision), ScanOperator.LessThanOrEqual, maxRevision)
            ],
            new DynamoDBOperationConfig
            {
                ConsistentRead = true, RetrieveDateTimeInUtc = true
            });
        var commits = await query.GetNextSetAsync(cancellationToken).ConfigureAwait(false);
        while (commits.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            foreach (var commit in commits)
            {
                yield return new Commit(
                    commit.BucketId,
                    commit.StreamId,
                    commit.StreamRevision,
                    Guid.Parse(commit.CommitId),
                    commit.CommitSequence,
                    DateTimeOffset.FromUnixTimeSeconds(commit.CommitStamp),
                    0,
                    _serializer.Deserialize<Dictionary<string, object>>(commit.Headers),
                    _serializer.Deserialize<List<EventMessage>>(commit.Events));
            }

            commits = await query.GetNextSetAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<ICommit?> Commit(
        IEventStream eventStream,
        Guid? commitId = null,
        CancellationToken cancellationToken = default)
    {
        var id = commitId ?? Guid.NewGuid();
        var attempt = DynamoDbCommit.FromStream(eventStream, id, _serializer);
        await _context.SaveAsync(attempt, cancellationToken).ConfigureAwait(false);
        return new Commit(attempt.BucketId, attempt.StreamId, attempt.StreamRevision, id,
            attempt.CommitSequence,
            DateTimeOffset.FromUnixTimeSeconds(attempt.CommitStamp),
            0,
            eventStream.UncommittedHeaders.ToDictionary(),
            eventStream.UncommittedEvents);
    }

    public async Task<ISnapshot?> GetSnapshot(
        string bucketId,
        string streamId,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        var query = _context.FromQueryAsync<DynamoDbCommit>(
            new QueryOperationConfig
            {
                BackwardSearch = true, ConsistentRead = true, Limit = 1,
                Filter = new QueryFilter(nameof(DynamoDbSnapshots.StreamRevision), QueryOperator.LessThanOrEqual,
                    [new AttributeValue { N = maxRevision.ToString() }])
            },
            new DynamoDBOperationConfig
            {
                ConsistentRead = true, RetrieveDateTimeInUtc = true, BackwardQuery = true,
            });
        var commits = await query.GetNextSetAsync(cancellationToken).ConfigureAwait(false);
        var commit = commits[0];
        return new Snapshot(
            commit.BucketId,
            commit.StreamId,
            commit.StreamRevision,
            _serializer.Deserialize<object>(commit.Events)!);
    }

    public async Task<bool> AddSnapshot(ISnapshot snapshot)
    {
        var attempt = DynamoDbSnapshots.FromSnapshot(snapshot, _serializer);
        await _context.SaveAsync(attempt).ConfigureAwait(false);
        return true;
    }
}
