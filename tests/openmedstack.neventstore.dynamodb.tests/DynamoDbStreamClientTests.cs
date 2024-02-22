using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenMedStack.Events;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.DynamoDbClient;
using OpenMedStack.NEventStore.Serialization;
using Xunit;

namespace OpenMedStack.NEventStore.DynamoDb.Tests;

public class DynamoDbStreamClientTests : IDisposable
{
    private readonly ManualResetEventSlim _resetEvent = new(false);
    private readonly StreamClient _streamClient;
    private readonly AmazonDynamoDBClient _dbClient;
    private readonly DynamoDbManagement _management;
    private readonly DynamoDbPersistenceEngine _engine;

    public DynamoDbStreamClientTests()
    {
        _dbClient = new AmazonDynamoDBClient(
            new BasicAWSCredentials("blah", "blah"),
            new AmazonDynamoDBConfig
            {
                AllowAutoRedirect = true,
                RegionEndpoint = RegionEndpoint.EUCentral1,
                ServiceURL = "http://localhost:8000"
            });
        _management = new DynamoDbManagement(_dbClient);
        _engine = new DynamoDbPersistenceEngine(_dbClient,
            new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance), new NullLogger<DynamoDbPersistenceEngine>());
        _streamClient = new DelegateStreamClient(new BasicAWSCredentials("blah", "blah"),
            new AmazonDynamoDBStreamsConfig
            {
                AllowAutoRedirect = true,
                ServiceURL = "http://localhost:8000",
                UseHttp = true
            },
            Handle,
            new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance));
    }

    private Task Handle(EventMessage message, CancellationToken cancellationToken)
    {
        if (message.Body is BaseEvent)
        {
            _resetEvent.Set();
        }

        return Task.CompletedTask;
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

    public void Dispose()
    {
        _resetEvent.Dispose();
        _dbClient.Dispose();
        _engine.Dispose();
        GC.SuppressFinalize(this);
    }
}

internal record TestEvent() : BaseEvent("id", DateTimeOffset.UtcNow);
