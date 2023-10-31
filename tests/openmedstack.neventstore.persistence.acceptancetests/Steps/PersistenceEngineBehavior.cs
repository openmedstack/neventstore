using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Persistence.InMemory;
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
        await PersistenceManagement.Drop().ConfigureAwait(false);
    }

    [Given("a (.+) persistence engine")]
    public void GivenAPersistenceEngine(string type)
    {
        var (commitEvents, accessSnapshots, managePersistence) = type switch
        {
            "in-memory" => CreateInMemoryPersistence(ConfiguredPageSizeForTesting),
            //"postgres" => SqlPersistenceEngineFactory.Create(2),
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
}
