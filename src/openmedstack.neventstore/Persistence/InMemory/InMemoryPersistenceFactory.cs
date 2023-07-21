using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Persistence.InMemory;

using Microsoft.Extensions.Logging;

public class InMemoryPersistenceFactory : IPersistenceFactory
{
    private readonly ILogger _logger;

    public InMemoryPersistenceFactory(ILogger logger)
    {
        _logger = logger;
    }

    public virtual IPersistStreams Build() => new InMemoryPersistenceEngine(_logger);
}