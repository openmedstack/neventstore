using System.Net;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.DynamoDb;

public class DynamoDbManagement : IManagePersistence
{
    private const string CommitsTableName = "commits";
    private const string SnapshotsTableName = "snapshots";
    private readonly IAmazonDynamoDB _dbClient;

    public DynamoDbManagement(IAmazonDynamoDB dbClient)
    {
        _dbClient = dbClient;
    }

    public async Task Initialize()
    {
        var tables = await _dbClient.ListTablesAsync().ConfigureAwait(false);

        if (!tables.TableNames.Contains(CommitsTableName))
        {
            await _dbClient.CreateTableAsync(new CreateTableRequest(CommitsTableName,
            [
                new(nameof(DynamoDbCommit.BucketAndStream), KeyType.HASH),
                new(nameof(DynamoDbCommit.CommitSequence), KeyType.RANGE)
            ])
            {
                AttributeDefinitions =
                [
                    new AttributeDefinition(nameof(DynamoDbCommit.BucketAndStream), ScalarAttributeType.S),
                    new AttributeDefinition(nameof(DynamoDbCommit.CommitSequence), ScalarAttributeType.N),
                    new AttributeDefinition(nameof(DynamoDbCommit.StreamRevision), ScalarAttributeType.N)
                ],
                LocalSecondaryIndexes =
                [
                    new LocalSecondaryIndex
                    {
                        IndexName = "RevisionIndex",
                        KeySchema =
                        [
                            new KeySchemaElement(nameof(DynamoDbCommit.BucketAndStream), KeyType.HASH),
                            new KeySchemaElement(nameof(DynamoDbCommit.StreamRevision), KeyType.RANGE)
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL }
                    }
                ],
                TableClass = TableClass.STANDARD,
                StreamSpecification = new StreamSpecification
                    { StreamEnabled = true, StreamViewType = StreamViewType.NEW_IMAGE },
                ProvisionedThroughput = new ProvisionedThroughput(1, 1)
            }).ConfigureAwait(false);
        }

        if (!tables.TableNames.Contains(SnapshotsTableName))
        {
            await _dbClient.CreateTableAsync(new CreateTableRequest(SnapshotsTableName,
                [
                    new(nameof(DynamoDbSnapshots.BucketAndStream), KeyType.HASH),
                    new(nameof(DynamoDbSnapshots.StreamRevision), KeyType.RANGE)
                ],
                [
                    new AttributeDefinition(nameof(DynamoDbSnapshots.BucketAndStream),
                        ScalarAttributeType.S),
                    new AttributeDefinition(nameof(DynamoDbSnapshots.StreamRevision), ScalarAttributeType.N)
                ],
                new ProvisionedThroughput(1, 1))).ConfigureAwait(false);
        }
    }

    public IAsyncEnumerable<ICommit> GetFrom(string bucketId, long checkpointToken, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public IAsyncEnumerable<IStreamHead> GetStreamsToSnapshot(
        string bucketId,
        int maxThreshold,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<bool> Purge(string bucketId)
    {
        throw new NotSupportedException();
    }

    public async Task<bool> Drop()
    {
        var responses = await Task.WhenAll(
            _dbClient.DeleteTableAsync(CommitsTableName),
            _dbClient.DeleteTableAsync(SnapshotsTableName)).ConfigureAwait(false);
        return responses.All(response => response.HttpStatusCode == HttpStatusCode.OK);
    }

    public async Task<bool> DeleteStream(string bucketId, string streamId)
    {
        var pk = $"{bucketId}{streamId}";
        var items = await _dbClient.QueryAsync(new QueryRequest
        {
            TableName = CommitsTableName,
            KeyConditionExpression = $"{nameof(DynamoDbCommit.BucketAndStream)} = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                { [":pk"] = new AttributeValue(pk) },
            Select = Select.SPECIFIC_ATTRIBUTES,
            ProjectionExpression = nameof(DynamoDbCommit.CommitSequence)
        }).ConfigureAwait(false);
        var response = await _dbClient.BatchWriteItemAsync(new Dictionary<string, List<WriteRequest>>
        {
            [CommitsTableName] = items.Items.Select(item => new WriteRequest
            {
                DeleteRequest = new DeleteRequest
                {
                    Key = new Dictionary<string, AttributeValue>
                    {
                        [nameof(DynamoDbCommit.BucketAndStream)] = new AttributeValue { S = pk },
                        [nameof(DynamoDbCommit.CommitSequence)] = item[nameof(DynamoDbCommit.CommitSequence)]
                    }
                }
            }).ToList()
        }).ConfigureAwait(false);
        return response.HttpStatusCode == HttpStatusCode.OK;
    }
}
