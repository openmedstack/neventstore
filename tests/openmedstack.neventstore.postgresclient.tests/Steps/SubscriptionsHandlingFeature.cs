using Microsoft.Extensions.Logging.Abstractions;
using NEventStore;
using Npgsql;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Persistence.Sql;
using OpenMedStack.NEventStore.Persistence.Sql.SqlDialects;
using OpenMedStack.NEventStore.Serialization;
using Xunit;

namespace OpenMedStack.NEventStore.PostgresClient.Tests.Steps;

[Binding]
public class SubscriptionsHandlingFeature : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ManualResetEventSlim _waitHandle = new();
    private PgPublicationClient _client = null!;
    private IStoreEvents _eventStore = null!;
    private Task _subscriptionTask = null!;

    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=eventstore;Username=postgres;Password=postgres";

    [Given(@"a postgres server for NEventStore")]
    public void GivenAPostgresServerForNEventStore()
    {
        _eventStore = OpenMedStack.NEventStore.Wireup.Init(NullLoggerFactory.Instance)
            .UsingSqlPersistence(NpgsqlFactory.Instance, ConnectionString)
            .WithDialect(new PostgreSqlDialect(NullLogger.Instance))
            .WithStreamIdHasher(new Sha1StreamIdHasher())
            .InitializeStorageEngine()
            .UsingJsonSerialization()
            .Build();
    }

    [Given(@"a commit publication")]
    public async Task GivenACommitPublication()
    {
        try
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE PUBLICATION commit_pub FOR TABLE Commits WITH (publish = 'insert')";
            var amount = await command.ExecuteNonQueryAsync();

            Assert.Equal(0, amount);
        }
        catch (PostgresException)
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

        _client = new DelegatePgPublicationClient(ConnectionString,
            new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance), Handler);
        await _client.CreateSubscriptionSlot(_cancellationTokenSource.Token);
        _subscriptionTask = _client.Subscribe(_cancellationTokenSource.Token);
    }

    [When(@"a row is inserted into a table")]
    public async Task WhenARowIsInsertedIntoATable()
    {
        var stream = await _eventStore.OpenStream(Guid.NewGuid(), 0);
        stream.Add(new EventMessage(new TestEvent { Value = DateTimeOffset.UtcNow.ToString("F") }));
        await stream.CommitChanges(Guid.NewGuid(), CancellationToken.None);
    }

    [Then(@"a notification is received")]
    public void ThenANotificationIsReceived()
    {
        var result = _waitHandle.Wait(2000);

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
