using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Divergic.Logging.Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.Events;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.DynamoDbClient;
using OpenMedStack.NEventStore.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace OpenMedStack.NEventStore.DynamoDb.Tests;

public class DynamoDbStreamClientTests : IAsyncDisposable
{
    private readonly ManualResetEventSlim _resetEvent = new(false);
    private readonly StreamClient _streamClient;
    private readonly AmazonDynamoDBClient _dbClient;
    private readonly DynamoDbManagement _management;
    private readonly DynamoDbPersistenceEngine _engine;

    public DynamoDbStreamClientTests(ITestOutputHelper output)
    {
        var logger = LogFactory.Create(output);
        var serviceUrl = "http://localhost:8000";
        _dbClient = new AmazonDynamoDBClient(
            new BasicAWSCredentials("blah", "blah"),
            new AmazonDynamoDBConfig
            {
                AllowAutoRedirect = true,
                RegionEndpoint = RegionEndpoint.EUCentral1,
                ServiceURL = serviceUrl,
                UseHttp = true
            });
        _management = new DynamoDbManagement(_dbClient, logger.CreateLogger<DynamoDbManagement>());
        _engine = new DynamoDbPersistenceEngine(_dbClient,
            new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance), new NullLogger<DynamoDbPersistenceEngine>());
        _streamClient = new DelegateStreamClient(new BasicAWSCredentials("blah", "blah"),
            new AmazonDynamoDBStreamsConfig
            {
                AllowAutoRedirect = true,
                ServiceURL = serviceUrl,
                UseHttp = true
            },
            Handle,
            new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance),
            logger.CreateLogger<DelegateStreamClient>());
    }

    private Task<HandlingResult> Handle(ICommit commit, CancellationToken cancellationToken)
    {
        var message = commit.Events.FirstOrDefault();
        if (message?.Body is BaseEvent)
        {
            _resetEvent.Set();
        }

        return Task.FromResult(HandlingResult.MoveToNext);
    }

    [Fact]
    public async Task ReceivesUpdateNotificationsWhenEventsArePersisted()
    {
        using var tokenSource = new CancellationTokenSource();
        var eventStream = OptimisticEventStream.Create(
            Guid.NewGuid().ToString("N"),
            "test",
            NullLogger<OptimisticEventStream>.Instance);
        eventStream.Add(new EventMessage(new TestEvent()));

        await _management.Initialize();
        await _streamClient.Subscribe(tokenSource.Token);
        await _engine.Commit(eventStream, Guid.NewGuid(), tokenSource.Token);

        var completed = _resetEvent.Wait(1000);
        Assert.True(completed);
        await tokenSource.CancelAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _management.Drop();
        await CastAndDispose(_resetEvent);
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

internal record TestEvent() : BaseEvent("id", DateTimeOffset.UtcNow);
