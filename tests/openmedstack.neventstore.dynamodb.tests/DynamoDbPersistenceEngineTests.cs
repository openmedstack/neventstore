using Amazon;
using Amazon.DynamoDBv2;
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
    private readonly DynamoDbPersistenceEngine _engine;
    private readonly DynamoDbManagement _management;

    public DynamoDbPersistenceEngineTests()
    {
        _dbClient = new AmazonDynamoDBClient(
            new BasicAWSCredentials("blah", "blah"),
            new AmazonDynamoDBConfig
            {
                AllowAutoRedirect = true,
                RegionEndpoint = RegionEndpoint.EUCentral1,
                ServiceURL = "http://localhost:8000"
            });

        _engine =
            new DynamoDbPersistenceEngine(_dbClient,
                new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance),
                NullLogger<DynamoDbPersistenceEngine>.Instance);
        _management = new DynamoDbManagement(_dbClient);
    }

    [Fact]
    public async Task CanCommitStream()
    {
        await _management.Initialize();
        var bucket = Guid.NewGuid().ToString("N");
        var streamId = Guid.NewGuid().ToString("N");

        var stream = OptimisticEventStream.Create(bucket, streamId, NullLogger<OptimisticEventStream>.Instance);
        stream.Add(new EventMessage(1));
        stream.Add(new EventMessage(2));
        var commitResult = await _engine.Commit(stream);

        Assert.NotNull(commitResult);
    }

    [Fact]
    public async Task WhenCommittingTwiceThenThrows()
    {
        await _management.Initialize();
        var bucket = Guid.NewGuid().ToString("N");
        var streamId = Guid.NewGuid().ToString("N");
        var commitId = Guid.NewGuid();
        var stream = OptimisticEventStream.Create(bucket, streamId, NullLogger<OptimisticEventStream>.Instance);
        stream.Add(new EventMessage(1));
        stream.Add(new EventMessage(2));
        _ = await _engine.Commit(stream, commitId);
        await Assert.ThrowsAsync<DuplicateCommitException>(() => _ = _engine.Commit(stream, commitId));
    }

    [Fact]
    public async Task WhenCommittingDuplicateCommitThenUpdatesStream()
    {
        await _management.Initialize();
        var bucket = Guid.NewGuid().ToString("N");
        var streamId = Guid.NewGuid().ToString("N");

        var stream = OptimisticEventStream.Create(bucket, streamId, NullLogger<OptimisticEventStream>.Instance);
        stream.Add(new EventMessage(1));
        stream.Add(new EventMessage(2));
        var result = await _engine.Commit(stream);
        stream.SetPersisted(result!.CommitSequence);
        stream.Add(new EventMessage(3));
        var stream2 = await OptimisticEventStream.Create(bucket, streamId, _engine, 0, int.MaxValue,
            NullLogger<OptimisticEventStream>.Instance);
        stream2.Add(new EventMessage(100));

        await _engine.Commit(stream);
        await _engine.Commit(stream2);

        var finalStream = await OptimisticEventStream.Create(bucket, streamId, _engine, 0, int.MaxValue,
            NullLogger<OptimisticEventStream>.Instance);

        Assert.Equal(4, finalStream.StreamRevision);
        Assert.Equal(3, finalStream.CommitSequence);
    }

    [Fact]
    public async Task CanCommitSnapshot()
    {
        await _management.Initialize();
        var bucket = Guid.NewGuid().ToString("N");
        var streamId = Guid.NewGuid().ToString("N");

        var snapshot = new Snapshot(bucket, streamId, 1, new EventMessage(1));
        var commitResult = await _engine.AddSnapshot(snapshot, CancellationToken.None);

        Assert.True(commitResult);
    }

    [Fact]
    public async Task CanLoadSnapshot()
    {
        await _management.Initialize();
        var bucket = Guid.NewGuid().ToString("N");
        var streamId = Guid.NewGuid().ToString("N");

        var snapshot = new Snapshot(bucket, streamId, 1, new EventMessage(1));
        var commitResult = await _engine.AddSnapshot(snapshot, CancellationToken.None);
        Assert.True(commitResult);

        var reloaded = await _engine.GetSnapshot(bucket, streamId, 1, default);
        Assert.NotNull(reloaded);
    }

    [Fact]
    public async Task CanRetrieveStreamAmongMany()
    {
        await _management.Initialize();
        var bucket = Guid.NewGuid().ToString("N");
        var streamId = Guid.NewGuid().ToString("N");
        var engine =
            new DynamoDbPersistenceEngine(_dbClient,
                new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance),
                NullLogger<DynamoDbPersistenceEngine>.Instance);
        var stream = OptimisticEventStream.Create(bucket, streamId, NullLogger<OptimisticEventStream>.Instance);
        stream.Add(new EventMessage(1));
        stream.Add(new EventMessage(2));
        var commitResult = await engine.Commit(stream);

        Assert.NotNull(commitResult);

        stream.SetPersisted(commitResult.CommitSequence);
        stream.Add(new EventMessage(3));
        await engine.Commit(stream);
        for (int i = 0; i < 100; i++)
        {
            var streamId2 = Guid.NewGuid().ToString("N");
            var stream2 = OptimisticEventStream.Create(bucket, streamId2, NullLogger<OptimisticEventStream>.Instance);
            stream2.Add(new EventMessage("a" + i));
            await engine.Commit(stream2);
        }

        var loaded = await engine.Get(bucket, streamId, 0, int.MaxValue, default).ToArray();
        Assert.Equal(2, loaded.Length);
        Assert.Equal(3, loaded[1].StreamRevision);
    }
}
