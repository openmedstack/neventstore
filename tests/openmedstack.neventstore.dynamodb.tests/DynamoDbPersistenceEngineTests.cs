using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using OpenMedStack.NEventStore.Serialization;
using Xunit;

namespace OpenMedStack.NEventStore.DynamoDb.Tests;

public class DynamoDbPersistenceEngineTests : IAsyncDisposable
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
        _management = new DynamoDbManagement(_dbClient, NullLogger<DynamoDbManagement>.Instance);
    }

    [Fact]
    public async Task CanCommitStream()
    {
        await _management.Initialize();
        var bucket = Guid.NewGuid().ToString("N");
        var streamId = Guid.NewGuid().ToString("N");

        var stream = new CommitAttempt(bucket, streamId, 1, Guid.NewGuid(), 1, DateTimeOffset.UtcNow,
            new Dictionary<string, object>(), [new EventMessage(1), new EventMessage(2)]);
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
        var stream = new CommitAttempt(bucket, streamId, 1, commitId, 1, DateTimeOffset.UtcNow,
            new Dictionary<string, object>(), [new EventMessage(1), new EventMessage(2)]);
        _ = await _engine.Commit(stream);
        await Assert.ThrowsAsync<DuplicateCommitException>(() => _ = _engine.Commit(stream));
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
        var stream = new CommitAttempt(bucket, streamId, 1, Guid.NewGuid(), 1, DateTimeOffset.UtcNow,
            new Dictionary<string, object>(), [new EventMessage(1), new EventMessage(2)]);
        var commitResult = await engine.Commit(stream);

        Assert.NotNull(commitResult);

        for (int i = 0; i < 100; i++)
        {
            var streamId2 = Guid.NewGuid().ToString("N");
            var stream2 = new CommitAttempt(bucket, streamId2, 1, Guid.NewGuid(), 1, DateTimeOffset.UtcNow,
                new Dictionary<string, object>(), [new EventMessage("a" + i)]);
            await engine.Commit(stream2);
        }

        var loaded = await engine.Get(bucket, streamId, 0, int.MaxValue, default).ToArray();
        Assert.Single(loaded);
        Assert.Equal(1, loaded[0].StreamRevision);
    }

    public async ValueTask DisposeAsync()
    {
        await _management.Drop();
        await CastAndDispose(_dbClient);
        await CastAndDispose(_engine);
        GC.SuppressFinalize(this);

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource.Dispose();
        }
    }
}
