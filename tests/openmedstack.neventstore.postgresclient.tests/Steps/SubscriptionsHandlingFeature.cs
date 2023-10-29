using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using OpenMedStack.NEventStore.Persistence.Sql;
using OpenMedStack.NEventStore.Persistence.Sql.SqlDialects;
using OpenMedStack.NEventStore.Serialization;
using Xunit;

namespace OpenMedStack.NEventStore.PostgresClient.Tests.Steps;

[Binding]
public class SubscriptionsHandlingFeature : IDisposable
{
    private IManagePersistence _managePersistence = null!;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ManualResetEventSlim _waitHandle = new();
    private PgPublicationClient _client = null!;
    private ICommitEvents _eventStore = null!;
    private Task _subscriptionTask = null!;

    private const string ConnectionString =
        "Server=localhost;Keepalive=1;Pooling=true;MinPoolSize=1;MaxPoolSize=20;Port=5432;Database=openmedstack;User Id=openmedstack;Password=openmedstack;";

    [Given(@"a postgres server for NEventStore")]
    public void GivenAPostgresServerForNEventStore()
    {
        var serviceCollection = new ServiceCollection()
            .RegisterJsonSerialization()
            .AddLogging()
            .RegisterSqlEventStore<PostgreSqlDialect, Sha1StreamIdHasher>(NpgsqlFactory.Instance, ConnectionString);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        _managePersistence = serviceProvider.GetRequiredService<IManagePersistence>();
        _managePersistence.Initialize();
        _eventStore = serviceProvider.GetRequiredService<ICommitEvents>();
    }

    [Given(@"a commit publication")]
    public async Task GivenACommitPublication()
    {
        try
        {
            var connection = new NpgsqlConnection(ConnectionString);
            await using var _ = connection.ConfigureAwait(false);
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            await using var __ = command.ConfigureAwait(false);
            command.CommandText =
                "CREATE PUBLICATION commit_pub FOR TABLE Commits WITH (publish = 'insert')";
            var amount = await command.ExecuteNonQueryAsync().ConfigureAwait(false);

            Assert.True(0 <= amount);
        }
        catch (PostgresException e) when (e.Code == "42710")
        {
        }
    }

    [Given(@"a postgres server subscription client")]
    public async Task GivenAPostgresServerSubscriptionClient()
    {
        Task Handler(Type type, object message, CancellationToken cancellationToken)
        {
            if (!_waitHandle.IsSet)
            {
                _waitHandle.Set();
            }

            return Task.CompletedTask;
        }

        _client = new DelegatePgPublicationClient("commit_slot", "commit_pub", ConnectionString,
            new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance), Handler);
        await _client.CreateSubscriptionSlot(_cancellationTokenSource.Token).ConfigureAwait(false);
        _subscriptionTask = _client.Subscribe(_cancellationTokenSource.Token);
    }

    [When(@"a row is inserted into a table")]
    public async Task WhenARowIsInsertedIntoATable()
    {
        var stream = await OptimisticEventStream.Create(Bucket.Default, Guid.NewGuid().ToString(), _eventStore, 0,
            int.MaxValue, NullLogger<OptimisticEventStream>.Instance).ConfigureAwait(false);
        stream.Add(new EventMessage(new TestEvent { Value = DateTimeOffset.UtcNow.ToString("F") }));
        var commit = await _eventStore.Commit(stream).ConfigureAwait(false);

        Assert.NotNull(commit);
    }

    [Then(@"a notification is received")]
    public void ThenANotificationIsReceived()
    {
        var result = _waitHandle.Wait(Debugger.IsAttached ? 30000 : 5000);

        Assert.True(result);
    }

    public void Dispose()
    {
        _waitHandle.Dispose();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _eventStore.Dispose();
        try
        {
            _subscriptionTask.Dispose();
            _managePersistence.Drop();
        }
        catch (InvalidOperationException)
        {
        }
    }
}

public class TestEvent
{
    public string? Value { get; set; }
}
