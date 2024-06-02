using System.Net;
using System.Runtime.CompilerServices;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.DynamoDb;

public class DynamoDbPersistenceEngine(
    IAmazonDynamoDB context,
    ISerialize serializer,
    ILogger<DynamoDbPersistenceEngine> logger) : ICommitEvents, IAccessSnapshots
{
    private const string SnapshotsTableName = "snapshots";
    private const string CommitsTableName = "commits";
    private const string CommitId = "CommitId";
    private bool _disposed;

    public void Dispose()
    {
        context.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async IAsyncEnumerable<ICommit> Get(
        string bucketId,
        string streamId,
        int minRevision,
        int maxRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ThrowWhenDisposed();
        maxRevision = maxRevision == int.MaxValue ? int.MaxValue : maxRevision + 1;
        minRevision = minRevision < 0 ? 0 : minRevision;
        var queryRequest = new QueryRequest
        {
            TableName = CommitsTableName, IndexName = "RevisionIndex", ConsistentRead = true,
            KeyConditionExpression =
                "BucketAndStream = :v_BucketAndStream AND StreamRevision BETWEEN :v_MinRevision AND :v_MaxRevision",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":v_BucketAndStream", new AttributeValue { S = $"{bucketId}{streamId}" } },
                { ":v_MinRevision", new AttributeValue { N = minRevision.ToString() } },
                { ":v_MaxRevision", new AttributeValue { N = maxRevision.ToString() } }
            },
            ScanIndexForward = true
        };
        var response = await context.QueryAsync(queryRequest, cancellationToken).ConfigureAwait(false);
        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            yield break;
        }

        var commits = response.Items;
        foreach (var commit in commits
            .TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        {
            yield return commit.ToCommit(serializer);
        }
    }

    public async Task<ICommit?> Commit(
        CommitAttempt commitAttempt,
        CancellationToken cancellationToken = default)
    {
        ThrowWhenDisposed();
        var attempt = DynamoDbCommit.FromCommitAttempt(commitAttempt, serializer);
        try
        {
            if (!await context.Save(attempt, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return new Commit(
                attempt.BucketId,
                attempt.StreamId,
                attempt.StreamRevision,
                Guid.Parse(attempt.CommitId),
                attempt.CommitSequence,
                DateTimeOffset.FromUnixTimeSeconds(attempt.CommitStamp),
                0,
                commitAttempt.Headers.ToDictionary(),
                commitAttempt.Events.ToList());
        }
        catch (ConditionalCheckFailedException e)
        {
            if (await DetectDuplicate(attempt).ConfigureAwait(false))
            {
                logger.LogError(e, "Duplicate commit detected");
                throw new DuplicateCommitException(e.Message, e);
            }

            logger.LogError(e, "Concurrent commit detected. Retrying");
            throw new ConcurrencyException(e.Message, e);
        }
    }

    private void ThrowWhenDisposed()
    {
        if (!_disposed)
        {
            return;
        }

        logger.LogWarning("Accessing a disposed object");
        throw new ObjectDisposedException("Already disposed");
    }

    private async Task<bool> DetectDuplicate(DynamoDbCommit attempt)
    {
        var queryRequest = new QueryRequest
        {
            TableName = CommitsTableName,
            ConsistentRead = true,
            ScanIndexForward = false,
            Limit = 1,
            Select = Select.SPECIFIC_ATTRIBUTES,
            ProjectionExpression = CommitId,
            KeyConditionExpression =
                "BucketAndStream = :v_BucketAndStream AND CommitSequence = :v_CommitSequence",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":v_BucketAndStream", new AttributeValue { S = $"{attempt.BucketId}{attempt.StreamId}" } },
                { ":v_CommitSequence", new AttributeValue { N = attempt.CommitSequence.ToString() } }
            }
        };
        var response = await context.QueryAsync(queryRequest).ConfigureAwait(false);
        var s = response.Items[0][CommitId].S;
        return response.HttpStatusCode == HttpStatusCode.OK && s == attempt.CommitId;
    }

    public async Task<ISnapshot?> GetSnapshot(
        string bucketId,
        string streamId,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        var queryRequest = new QueryRequest
        {
            TableName = SnapshotsTableName,
            ConsistentRead = true,
            Limit = 1,
            KeyConditionExpression =
                "BucketAndStream = :v_BucketAndStream AND StreamRevision <= :v_MaxRevision",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":v_BucketAndStream", new AttributeValue { S = $"{bucketId}{streamId}" } },
                { ":v_MaxRevision", new AttributeValue { N = maxRevision.ToString() } }
            },
            ScanIndexForward = false
        };
        var response = await context.QueryAsync(queryRequest, cancellationToken).ConfigureAwait(false);
        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            return null;
        }

        var commit = response.Items[0];

        return new Snapshot(
            commit["BucketId"].S,
            commit["StreamId"].S,
            int.Parse(commit["StreamRevision"].N),
            serializer.Deserialize<object>(commit["Payload"].B)!);
    }

    public async Task<bool> AddSnapshot(ISnapshot snapshot, CancellationToken cancellationToken)
    {
        var attempt = DynamoDbSnapshots.FromSnapshot(snapshot, serializer);
        return await context.Save(attempt, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
