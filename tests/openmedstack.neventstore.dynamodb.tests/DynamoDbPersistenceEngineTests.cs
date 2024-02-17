using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using OpenMedStack.NEventStore.Serialization;
using Xunit;

namespace OpenMedStack.NEventStore.DynamoDb.Tests;

public class DynamoDbPersistenceEngineTests
{
    private readonly AmazonDynamoDBClient _dbClient;
    private readonly DynamoDBContext _context;
    private readonly DynamoDbPersistenceEngine _engine;

    public DynamoDbPersistenceEngineTests()
    {
        _dbClient = new AmazonDynamoDBClient(
            new BasicAWSCredentials("blah", "blah"),
            new AmazonDynamoDBConfig
            {
                AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.EUCentral1,
                ServiceURL = "http://localhost:8000"
            });

        _context = new DynamoDBContext(_dbClient);
        _engine =
            new DynamoDbPersistenceEngine(_context, new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance));
    }

    [Fact]
    public async Task CanCommitStream()
    {
        await CreateTable(_dbClient);
        var bucket = Guid.NewGuid().ToString("N");
        var streamId = Guid.NewGuid().ToString("N");

        var stream = OptimisticEventStream.Create(bucket, streamId, NullLogger<OptimisticEventStream>.Instance);
        stream.Add(new EventMessage(1));
        stream.Add(new EventMessage(2));
        var commitResult = await _engine.Commit(stream);

        Assert.NotNull(commitResult);
    }

    [Fact]
    public async Task CanCommitSnapshot()
    {
        await CreateTable(_dbClient);
        var bucket = Guid.NewGuid().ToString("N");
        var streamId = Guid.NewGuid().ToString("N");

        var snapshot = new Snapshot(bucket, streamId, 1, new EventMessage(1));
        var commitResult = await _engine.AddSnapshot(snapshot);

        Assert.True(commitResult);
    }

    [Fact]
    public async Task CanLoadSnapshot()
    {
        await CreateTable(_dbClient);
        var bucket = Guid.NewGuid().ToString("N");
        var streamId = Guid.NewGuid().ToString("N");

        var snapshot = new Snapshot(bucket, streamId, 1, new EventMessage(1));
        var commitResult = await _engine.AddSnapshot(snapshot);
        Assert.True(commitResult);

        var reloaded = await _engine.GetSnapshot(bucket, streamId, 1, default);
        Assert.NotNull(reloaded);
    }

    [Fact]
    public async Task CanRetrieveAmongMany()
    {
        await CreateTable(_dbClient);
        var bucket = Guid.NewGuid().ToString("N");
        var streamId = Guid.NewGuid().ToString("N");
        var streamId2 = Guid.NewGuid().ToString("N");
        var engine =
            new DynamoDbPersistenceEngine(_context, new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance));
        var stream = OptimisticEventStream.Create(bucket, streamId, NullLogger<OptimisticEventStream>.Instance);
        stream.Add(new EventMessage(1));
        stream.Add(new EventMessage(2));
        var commitResult = await engine.Commit(stream);

        Assert.NotNull(commitResult);

        stream.SetPersisted(commitResult.CommitSequence);
        stream.Add(new EventMessage(3));
        await engine.Commit(stream);

        var stream2 = OptimisticEventStream.Create(bucket, streamId2, NullLogger<OptimisticEventStream>.Instance);
        stream2.Add(new EventMessage("a"));
        await engine.Commit(stream2);

        var loaded = await engine.Get(bucket, streamId, 0, int.MaxValue, default).ToArray();
        Assert.Equal(2, loaded.Length);
    }

    private static async Task CreateTable(AmazonDynamoDBClient dbClient)
    {
        var tables = await dbClient.ListTablesAsync();

        if (!tables.TableNames.Contains("commits"))
        {
            await dbClient.CreateTableAsync(new CreateTableRequest("commits",
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
                ProvisionedThroughput = new ProvisionedThroughput(1, 1)
            });
        }

        if (!tables.TableNames.Contains("snapshots"))
        {
            await dbClient.CreateTableAsync(new CreateTableRequest("snapshots",
                [
                    new(nameof(DynamoDbSnapshots.BucketAndStream), KeyType.HASH),
                    new(nameof(DynamoDbSnapshots.StreamRevision), KeyType.RANGE)
                ],
                [
                    new AttributeDefinition(nameof(DynamoDbSnapshots.BucketAndStream),
                        ScalarAttributeType.S),
                    new AttributeDefinition(nameof(DynamoDbSnapshots.StreamRevision), ScalarAttributeType.N)
                ],
                new ProvisionedThroughput(1, 1)));
        }
    }
}
