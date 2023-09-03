using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Persistence.InMemory;

using Microsoft.Extensions.Logging;

public class InMemoryPersistenceFactory : IPersistenceFactory
{
    private readonly ILogger<InMemoryPersistenceEngine> _logger;

    public InMemoryPersistenceFactory(ILogger<InMemoryPersistenceEngine> logger)
    {
        _logger = logger;
    }

    public virtual IPersistStreams Build() => new InMemoryPersistenceEngine(_logger);
}
