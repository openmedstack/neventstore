using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
    private IContainer? _testContainer;
    private IManagePersistence _managePersistence = null!;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ManualResetEventSlim _waitHandle = new();
    private PgPublicationClient _client = null!;
    private ICommitEvents _eventStore = null!;
    private Task _subscriptionTask = null!;

    private string? _connectionString;

    [AfterScenario]
    public async Task AfterScenario()
    {
        try
        {
            if (_testContainer != null)
            {
                await _testContainer.StopAsync().ConfigureAwait(false);
                await _testContainer.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            // Empty
        }
    }

    [Given("a postgres instance")]
    public async Task GivenAPersistenceEngine()
    {
        var testContainer = new ContainerBuilder()
            .WithImage("postgres:alpine")
            .WithPortBinding("5432", true)
            .WithEnvironment("POSTGRES_USER", "openmedstack")
            .WithEnvironment("POSTGRES_PASSWORD", "openmedstack")
            .WithEnvironment("POSTGRES_DB", "openmedstack")
            .WithCommand("postgres", "-c", "wal_level=logical")
            .Build();
        await testContainer.StartAsync().ConfigureAwait(false);
        _testContainer = testContainer;
        var mappedPublicPort = testContainer.GetMappedPublicPort(5432);
        _connectionString =
            $"Server=localhost;Keepalive=1;Pooling=true;MinPoolSize=1;MaxPoolSize=20;Port={mappedPublicPort};Database=openmedstack;User Id=openmedstack;Password=openmedstack;";
    }

    [Given(@"a postgres server for NEventStore")]
    public async Task GivenAPostgresServerForNEventStore()
    {
        var serviceCollection = new ServiceCollection()
            .RegisterJsonSerialization()
            .AddLogging()
            .RegisterSqlEventStore<PostgreSqlDialect, Sha256StreamIdHasher>(NpgsqlFactory.Instance, _connectionString!);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        _managePersistence = serviceProvider.GetRequiredService<IManagePersistence>();
        await _managePersistence.Initialize().ConfigureAwait(false);
        _eventStore = serviceProvider.GetRequiredService<ICommitEvents>();
    }

    [Given(@"a commit publication")]
    public async Task GivenACommitPublication()
    {
        try
        {
            var connection = new NpgsqlConnection(_connectionString);
            await using var connection1 = connection.ConfigureAwait(false);
            await connection.OpenAsync().ConfigureAwait(false);
            var command = connection.CreateCommand();
            await using var command1 = command.ConfigureAwait(false);
            command.CommandText =
                "CREATE PUBLICATION commit_pub FOR TABLE Commits WITH (publish = 'insert')";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (PostgresException e) when (e.SqlState == "42710")
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

        _client = new DelegatePgPublicationClient("commit_slot", "commit_pub", _connectionString!,
            new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance), Handler);
        await _client.CreateSubscriptionSlot(_cancellationTokenSource.Token).ConfigureAwait(false);
        _subscriptionTask = _client.Subscribe(_cancellationTokenSource.Token);
    }

    [When(@"a row is inserted into a table")]
    public async Task WhenARowIsInsertedIntoATable()
    {
        var stream = new CommitAttempt(Bucket.Default, Guid.NewGuid().ToString(), 1,
            Guid.NewGuid(), 1, DateTimeOffset.UtcNow, new Dictionary<string, object>(),
            [new EventMessage(new TestEvent { Value = DateTimeOffset.UtcNow.ToString("F") })]);
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

        GC.SuppressFinalize(this);
    }
}
