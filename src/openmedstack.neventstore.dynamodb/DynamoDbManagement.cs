using System.Net;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.DynamoDb;

public class DynamoDbManagement(IAmazonDynamoDB dbClient, ILogger<DynamoDbManagement> logger) : IManagePersistence
{
    private const string CommitsTableName = "commits";
    private const string SnapshotsTableName = "snapshots";

    public async Task Initialize()
    {
        var tables = await dbClient.ListTablesAsync().ConfigureAwait(false);
        logger.LogInformation("Checking for existing tables among {ExistingTables}",
            string.Join(", ", tables.TableNames));

        if (!tables.TableNames.Contains(CommitsTableName))
        {
            var createTableRequest = new CreateTableRequest(CommitsTableName,
            [
                new(nameof(DynamoDbCommit.TenantAndStream), KeyType.HASH),
                new(nameof(DynamoDbCommit.CommitSequence), KeyType.RANGE)
            ])
            {
                AttributeDefinitions =
                [
                    new AttributeDefinition(nameof(DynamoDbCommit.TenantAndStream), ScalarAttributeType.S),
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
                            new KeySchemaElement(nameof(DynamoDbCommit.TenantAndStream), KeyType.HASH),
                            new KeySchemaElement(nameof(DynamoDbCommit.StreamRevision), KeyType.RANGE)
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL }
                    }
                ],
                TableClass = TableClass.STANDARD,
                StreamSpecification = new StreamSpecification
                    { StreamEnabled = true, StreamViewType = StreamViewType.NEW_IMAGE },
                ProvisionedThroughput = new ProvisionedThroughput(1, 1)
            };
            try
            {
                await dbClient.CreateTableAsync(createTableRequest).ConfigureAwait(false);
            }
            catch (ResourceInUseException e)
            {
                logger.LogError(e,
                    "Failed to create table {TableName}. Name not found among {ExistingTables} before creating",
                    CommitsTableName, string.Join(", ", tables.TableNames));
            }
        }

        if (!tables.TableNames.Contains(SnapshotsTableName))
        {
            var createTableRequest = new CreateTableRequest(SnapshotsTableName,
                [
                    new(nameof(DynamoDbSnapshots.TenantAndStream), KeyType.HASH),
                    new(nameof(DynamoDbSnapshots.StreamRevision), KeyType.RANGE)
                ],
                [
                    new AttributeDefinition(nameof(DynamoDbSnapshots.TenantAndStream),
                        ScalarAttributeType.S),
                    new AttributeDefinition(nameof(DynamoDbSnapshots.StreamRevision), ScalarAttributeType.N)
                ],
                new ProvisionedThroughput(1, 1));
            try
            {
                await dbClient.CreateTableAsync(createTableRequest).ConfigureAwait(false);
            }
            catch (ResourceInUseException e)
            {
                logger.LogError(e,
                    "Failed to create table {TableName}. Name not found among {ExistingTables} before creating",
                    SnapshotsTableName, string.Join(", ", tables.TableNames));
            }
        }
    }

    public IAsyncEnumerable<ICommit> GetFrom(string TenantId, long checkpointToken, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public IAsyncEnumerable<IStreamHead> GetStreamsToSnapshot(
        string TenantId,
        int maxThreshold,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<bool> Purge(string TenantId)
    {
        throw new NotSupportedException();
    }

    public async Task<bool> Drop()
    {
        logger.LogInformation("Dropping tables {Commits} and {Snapshots}", CommitsTableName, SnapshotsTableName);
        var responses = await Task.WhenAll(
            dbClient.DeleteTableAsync(CommitsTableName),
            dbClient.DeleteTableAsync(SnapshotsTableName)).ConfigureAwait(false);
        return responses.All(response => response.HttpStatusCode == HttpStatusCode.OK);
    }

    public async Task<bool> DeleteStream(string TenantId, string streamId)
    {
        var pk = $"{TenantId}{streamId}";
        var items = await dbClient.QueryAsync(new QueryRequest
        {
            TableName = CommitsTableName,
            KeyConditionExpression = $"{nameof(DynamoDbCommit.TenantAndStream)} = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                { [":pk"] = new AttributeValue(pk) },
            Select = Select.SPECIFIC_ATTRIBUTES,
            ProjectionExpression = nameof(DynamoDbCommit.CommitSequence)
        }).ConfigureAwait(false);
        var response = await dbClient.BatchWriteItemAsync(new Dictionary<string, List<WriteRequest>>
        {
            [CommitsTableName] = items.Items.Select(item => new WriteRequest
            {
                DeleteRequest = new DeleteRequest
                {
                    Key = new Dictionary<string, AttributeValue>
                    {
                        [nameof(DynamoDbCommit.TenantAndStream)] = new AttributeValue { S = pk },
                        [nameof(DynamoDbCommit.CommitSequence)] = item[nameof(DynamoDbCommit.CommitSequence)]
                    }
                }
            }).ToList()
        }).ConfigureAwait(false);
        return response.HttpStatusCode == HttpStatusCode.OK;
    }
}
