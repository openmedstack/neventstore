namespace OpenMedStack.NEventStore.Tests.Persistence.InMemory;

using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Persistence.AcceptanceTests;
using OpenMedStack.NEventStore.Persistence.InMemory;

public class PersistenceEngineFixture : PersistenceEngineFixtureBase
{
    public PersistenceEngineFixture()
    {
        CreatePersistence = _ =>
            new InMemoryPersistenceEngine(NullLogger<InMemoryPersistenceEngine>.Instance);
    }
}
