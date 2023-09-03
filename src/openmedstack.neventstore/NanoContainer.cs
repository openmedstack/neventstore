namespace OpenMedStack.NEventStore;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

public class NanoContainer
{
    private readonly ILogger<NanoContainer> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly IDictionary<Type, ContainerRegistration> _registrations =
        new Dictionary<Type, ContainerRegistration>();

    public NanoContainer(ILoggerFactory logger)
    {
        _loggerFactory = logger;
        _logger = logger.CreateLogger<NanoContainer>();
        Register(logger);
        Register(logger.CreateLogger("EventStore"));
    }

    public virtual ContainerRegistration? Register<TService>(Func<NanoContainer, TService?> resolve)
        where TService : class
    {
        _logger.LogDebug(Messages.RegisteringWireupCallback, typeof(TService));
        var registration = new ContainerRegistration(c => (object?)resolve(c), _loggerFactory);
        _registrations[typeof(TService)] = registration;
        return registration;
    }

    public ContainerRegistration Register<TService>(TService instance)
    {
        if (Equals(instance, null))
        {
            throw new ArgumentNullException(nameof(instance), Messages.InstanceCannotBeNull);
        }

        if (!typeof(TService).IsValueType && !typeof(TService).IsInterface)
        {
            throw new ArgumentException(Messages.TypeMustBeInterface, nameof(instance));
        }

        _logger.LogDebug(Messages.RegisteringServiceInstance, typeof(TService));
        var registration = new ContainerRegistration(instance, _logger);
        _registrations[typeof(TService)] = registration;
        return registration;
    }

    public virtual TService? Resolve<TService>()
    {
        _logger.LogDebug(Messages.ResolvingService, typeof(TService));

        if (_registrations.TryGetValue(typeof(TService), out var registration))
        {
            var resolved = registration.Resolve(this);
            return resolved == null ? default : (TService?)resolved;
        }

        _logger.LogDebug(Messages.UnableToResolve, typeof(TService));
        return default;
    }
}

public class ContainerRegistration
{
    private readonly ILogger _logger;
    private readonly Func<NanoContainer, object?>? _resolve;
    private object? _instance;
    private bool _instancePerCall;

    public ContainerRegistration(Func<NanoContainer, object?> resolve, ILoggerFactory logger)
    {
        _logger = logger.CreateLogger<ContainerRegistration>();
        _logger.LogTrace(Messages.AddingWireupCallback);
        _resolve = resolve;
    }

    public ContainerRegistration(object instance, ILogger logger)
    {
        _logger = logger;
        _logger.LogTrace(Messages.AddingWireupRegistration, instance.GetType());
        _instance = instance;
    }

    public virtual ContainerRegistration InstancePerCall()
    {
        _logger.LogTrace(Messages.ConfiguringInstancePerCall);
        _instancePerCall = true;
        return this;
    }

    public virtual object? Resolve(NanoContainer container)
    {
        _logger.LogTrace(Messages.ResolvingInstance);
        if (_instancePerCall)
        {
            _logger.LogTrace(Messages.BuildingNewInstance);
            return _resolve?.Invoke(container);
        }

        _logger.LogTrace(Messages.AttemptingToResolveInstance);

        if (_instance != null)
        {
            return _instance;
        }

        _logger.LogTrace(Messages.BuildingAndStoringNewInstance);
        return _instance = _resolve!(container);
    }
}
