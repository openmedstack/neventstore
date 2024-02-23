using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.DynamoDb;
using OpenMedStack.NEventStore.Persistence.InMemory;
using OpenMedStack.NEventStore.Persistence.Sql;
using OpenMedStack.NEventStore.Persistence.Sql.SqlDialects;
using OpenMedStack.NEventStore.Serialization;
using TechTalk.SpecFlow;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

[Binding]
[Scope(Feature = "Persistence Engine Behavior")]
public partial class PersistenceEngineBehavior
{
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
        }
        catch
        {
            // Empty
        }
    }

    [Given("a (.+) persistence engine")]
    public void GivenAPersistenceEngine(string type)
    {
        var (commitEvents, accessSnapshots, managePersistence) = type switch
        {
            "in-memory" => CreateInMemoryPersistence(ConfiguredPageSizeForTesting),
            "postgres" => CreatePostgresPersistence(ConfiguredPageSizeForTesting),
            "dynamodb" => CreateDynamoDbPersistence(),
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

    private (ICommitEvents, IAccessSnapshots, IManagePersistence) CreateInMemoryPersistence(int pageSize)
    {
        var engine = new InMemoryPersistenceEngine(NullLogger<InMemoryPersistenceEngine>.Instance);
        return (engine, engine, engine);
    }

    private (ICommitEvents, IAccessSnapshots, IManagePersistence) CreatePostgresPersistence(int pageSize)
    {
        var engine = new SqlPersistenceEngine(
            new NetStandardConnectionFactory(
                NpgsqlFactory.Instance,
                "Server=localhost;Port=5432;Database=openmedstack;User Id=openmedstack;Password=openmedstack;",
                NullLogger<NetStandardConnectionFactory>.Instance),
            new PostgreSqlDialect(NullLogger<PostgreSqlDialect>.Instance),
            new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance),
            pageSize,
            new Sha1StreamIdHasher(),
            NullLogger<SqlPersistenceEngine>.Instance);
        return (engine, engine, engine);
    }


    private (ICommitEvents commitEvents, IAccessSnapshots accessSnapshots, IManagePersistence managePersistence)
        CreateDynamoDbPersistence()
    {
        var client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("blah", "blah"),
            new AmazonDynamoDBConfig
            {
                AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.EUCentral1,
                ServiceURL = "http://localhost:8000"
            });
        var engine = new DynamoDbPersistenceEngine(
            client,
            new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance),
            NullLogger<DynamoDbPersistenceEngine>.Instance);
        var management = new DynamoDbManagement(client, NullLogger<DynamoDbManagement>.Instance);
        return (engine, engine, management);
    }
}
