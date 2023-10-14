namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests;

using OpenMedStack.NEventStore.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

public class PersistenceEngineFixture : PersistenceEngineFixtureBase
{
    public PersistenceEngineFixture()
    {
        CreatePersistence = _ =>
        {
            var engine = new InMemoryPersistenceEngine(NullLogger<InMemoryPersistenceEngine>.Instance);
            return (engine, engine, engine);
        };
    }
}
