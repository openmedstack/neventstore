using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore;

using Microsoft.Extensions.Logging;
using Persistence.InMemory;

public static class PersistenceWireupExtensions
{
    public static PersistenceWireup UsingInMemoryPersistence(this Wireup wireup)
    {
        wireup.Logger.LogInformation(Resources.WireupSetPersistenceEngine, "InMemoryPersistenceEngine");
        wireup.With<IPersistStreams>(new InMemoryPersistenceEngine(wireup.Logger));

        return new PersistenceWireup(wireup);
    }

    public static int Records(this int records) => records;
}