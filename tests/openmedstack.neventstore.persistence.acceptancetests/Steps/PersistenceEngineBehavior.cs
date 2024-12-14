using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.DynamoDb;
using OpenMedStack.NEventStore.Persistence.InMemory;
using OpenMedStack.NEventStore.Persistence.Sql;
using OpenMedStack.NEventStore.Persistence.Sql.SqlDialects;
using OpenMedStack.NEventStore.Serialization;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

[Binding]
[Scope(Feature = "Persistence Engine Behavior")]
public partial class PersistenceEngineBehavior
{
    private IContainer? _testContainer;
    protected const int ConfiguredPageSizeForTesting = 2;
    public ICommitEvents Persistence { get; protected set; } = null!;
    public IAccessSnapshots Snapshots { get; protected set; } = null!;
    public IManagePersistence PersistenceManagement { get; protected set; } = null!;

    [AfterScenario]
    public async Task AfterScenario()
    {
        try
        {
            await PersistenceManagement.Drop().ConfigureAwait(false);
            if (_testContainer != null)
            {
                await _testContainer.StopAsync();
                await _testContainer.DisposeAsync();
            }
        }
        catch
        {
            // Empty
        }
    }

    [Given("a (.+) persistence engine")]
    public async Task GivenAPersistenceEngine(string type)
    {
        var (commitEvents, accessSnapshots, managePersistence) = type switch
        {
            "in-memory" => CreateInMemoryPersistence(),
            "postgres" => await CreatePostgresPersistence(ConfiguredPageSizeForTesting),
            "dynamodb" => await CreateDynamoDbPersistence(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        Persistence = commitEvents;
        PersistenceManagement = managePersistence;
        Snapshots = accessSnapshots;
    }

    [Given("the persistence is initialized")]
    public async Task GivenThePersistenceIsInitialized()
    {
        await PersistenceManagement.Initialize();
    }

    [Given("an existing commit attempt")]
    public void GivenAnExistingCommitAttempt()
    {
        _attempt = new CommitAttempt(
            BucketId,
            StreamId,
            1,
            Guid.NewGuid(),
            1,
            DateTimeOffset.UtcNow,
            new Dictionary<string, object>(),
            new List<EventMessage>
            {
                new(new ExtensionMethods.SomeDomainEvent { SomeProperty = "test" },
                    new Dictionary<string, object>())
            });
    }

    [When("committing again on the same stream")]
    public async Task WhenCommittingAgainOnTheSameStream()
    {
        await Persistence.Commit(_attempt);
    }

    [Then(@"should throw a duplicate commit exception")]
    public async Task ThenShouldThrowADuplicateCommitException()
    {
        await Assert.ThrowsAsync<DuplicateCommitException>(async () => await Persistence.Commit(_attempt));
    }

    private (ICommitEvents, IAccessSnapshots, IManagePersistence) CreateInMemoryPersistence()
    {
        var engine = new InMemoryPersistenceEngine(NullLogger<InMemoryPersistenceEngine>.Instance);
        return (engine, engine, engine);
    }

    private async Task<(ICommitEvents, IAccessSnapshots, IManagePersistence)> CreatePostgresPersistence(int pageSize)
    {
        var testContainer = new ContainerBuilder()
            .WithImage("postgres:alpine")
            .WithPortBinding("5432", true)
            .WithEnvironment("POSTGRES_USER", "openmedstack")
            .WithEnvironment("POSTGRES_PASSWORD", "openmedstack")
            .WithEnvironment("POSTGRES_DB", "openmedstack")
            .Build();
        await testContainer.StartAsync();
        _testContainer = testContainer;
        var mappedPublicPort = testContainer.GetMappedPublicPort(5432);
        var engine = new SqlPersistenceEngine(
            new NetStandardConnectionFactory(
                NpgsqlFactory.Instance,
                $"Server=localhost;Port={mappedPublicPort};Database=openmedstack;User Id=openmedstack;Password=openmedstack;",
                NullLogger<NetStandardConnectionFactory>.Instance),
            new PostgreSqlDialect(NullLogger<PostgreSqlDialect>.Instance),
            new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance),
            pageSize,
            new Sha256StreamIdHasher(),
            NullLogger<SqlPersistenceEngine>.Instance);
        return (engine, engine, engine);
    }

    private async Task<(ICommitEvents commitEvents, IAccessSnapshots accessSnapshots, IManagePersistence
            managePersistence)>
        CreateDynamoDbPersistence()
    {
        var testContainer = new ContainerBuilder()
            .WithImage("amazon/dynamodb-local:latest")
            .WithPortBinding("8000", true)
            .Build();
        await testContainer.StartAsync();
        var mappedPort = testContainer.GetMappedPublicPort(8000);
        _testContainer = testContainer;
        var client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("blah", "blah"),
            new AmazonDynamoDBConfig
            {
                AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.EUCentral1,
                ServiceURL = $"http://localhost:{mappedPort}"
            });
        var engine = new DynamoDbPersistenceEngine(
            client,
            new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance),
            NullLogger<DynamoDbPersistenceEngine>.Instance);
        var management = new DynamoDbManagement(client, NullLogger<DynamoDbManagement>.Instance);
        return (engine, engine, management);
    }
}
