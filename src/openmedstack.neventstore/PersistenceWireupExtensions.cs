using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore;

using Microsoft.Extensions.Logging;
using Persistence.InMemory;

public static class PersistenceWireupExtensions
{
    public static PersistenceWireup UsingInMemoryPersistence(this Wireup wireup)
    {
        wireup.Logger.CreateLogger<PersistenceWireup>()
            .LogInformation(Resources.WireupSetPersistenceEngine, "InMemoryPersistenceEngine");
        wireup.With<IPersistStreams>(
            new InMemoryPersistenceEngine(wireup.Logger.CreateLogger<InMemoryPersistenceEngine>()));

        return new PersistenceWireup(wireup);
    }

    public static int Records(this int records) => records;
}
