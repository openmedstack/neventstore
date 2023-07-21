
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore;

using System;
using Microsoft.Extensions.Logging;

public class PersistenceWireup : Wireup
{
    private readonly ILogger _logger;
    private bool _initialize;

    public PersistenceWireup(Wireup inner)
        : base(inner)
    {
        _logger = inner.Logger;
    }

    public virtual PersistenceWireup WithPersistence(IPersistStreams instance)
    {
        _logger.LogInformation(Messages.RegisteringPersistenceEngine, instance.GetType());
        With(instance);
        return this;
    }

    public virtual PersistenceWireup InitializeStorageEngine()
    {
        _logger.LogInformation(Messages.ConfiguringEngineInitialization);
        _initialize = true;
        return this;
    }

    public override IStoreEvents Build()
    {
        _logger.LogInformation(Messages.BuildingEngine);
        var engine = Container.Resolve<IPersistStreams>()
         ?? throw new Exception($"Could not resolve {nameof(IPersistStreams)}");

        if (_initialize)
        {
            _logger.LogDebug(Messages.InitializingEngine);
            engine.Initialize();
        }

        return base.Build();
    }
}