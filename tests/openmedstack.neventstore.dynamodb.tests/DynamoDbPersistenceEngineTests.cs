using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.DynamoDb;
using OpenMedStack.NEventStore.Serialization;
using Xunit;

namespace openmedstack.neventstore.dynamodb.tests;

public class DynamoDbPersistenceEngineTests
{
    [Fact]
    public async Task CanCommitStream()
    {
        var dbClient = new AmazonDynamoDBClient(
            new BasicAWSCredentials("blah", "blah"),
            new AmazonDynamoDBConfig
            {
                AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.EUCentral1,
                ServiceURL = "http://localhost:8000"
            });
//        await CreateTable(dbClient);
        var context =
            new DynamoDBContext(
                dbClient);
        var bucket = Guid.NewGuid().ToString("N");
        var streamId = Guid.NewGuid().ToString("N");
        var streamId2 = Guid.NewGuid().ToString("N");
        var engine =
            new DynamoDbPersistenceEngine(context, new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance));
        var stream = OptimisticEventStream.Create(bucket, streamId, NullLogger<OptimisticEventStream>.Instance);
        stream.Add(new EventMessage(1));
        stream.Add(new EventMessage(2));
        var commitResult = await engine.Commit(stream);

        Assert.NotNull(commitResult);

        stream.SetPersisted(commitResult!.CommitSequence);
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
        await dbClient.CreateTableAsync(new CreateTableRequest("commits",
        [
            new(nameof(DynamoDbCommit.BucketAndStreamAndSequence), KeyType.HASH),
            new(nameof(DynamoDbCommit.StreamRevision), KeyType.RANGE)
        ])
        {
            AttributeDefinitions =
            [
                new AttributeDefinition(nameof(DynamoDbCommit.BucketAndStreamAndSequence), ScalarAttributeType.S),
                new AttributeDefinition(nameof(DynamoDbCommit.StreamRevision), ScalarAttributeType.N)
            ],
            ProvisionedThroughput = new ProvisionedThroughput(1, 1)
        });
    }
}
