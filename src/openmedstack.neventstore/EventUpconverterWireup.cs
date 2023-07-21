using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMedStack.NEventStore.Conversion;

namespace OpenMedStack.NEventStore;

using Microsoft.Extensions.Logging;

public class EventUpconverterWireup : Wireup
{
    private readonly ILogger _logger;
    private readonly List<Assembly> _assembliesToScan = new();

    private readonly IDictionary<Type, Func<object, object>> _registered =
        new Dictionary<Type, Func<object, object>>();

    public EventUpconverterWireup(Wireup wireup, ILogger logger)
        : base(wireup)
    {
        _logger = logger;
        _logger.LogDebug(Messages.EventUpconverterRegistered);

        Container.Register(
            _ =>
            {
                if (_registered.Count > 0)
                {
                    return new EventUpconverterPipelineHook(_registered, logger);
                }

                if (!_assembliesToScan.Any())
                {
                    _assembliesToScan.AddRange(GetAllAssemblies());
                }

                var converters = GetConverters(_assembliesToScan);
                return new EventUpconverterPipelineHook(converters, logger);
            });
    }

    private static IEnumerable<Assembly> GetAllAssemblies()
    {
        return Assembly.GetCallingAssembly()
            .GetReferencedAssemblies()
            .Select(Assembly.Load)
            .Concat(new[] {Assembly.GetCallingAssembly()});
    }

    private static IDictionary<Type, Func<object, object>> GetConverters(IEnumerable<Assembly> toScan)
    {
        var c = from a in toScan
                from t in a.GetTypes()
                where !t.IsAbstract
                let i = t.GetInterface(typeof(IUpconvertEvents<,>).FullName!)
                where i != null
                let sourceType = i.GetGenericArguments().First()
                let convertMethod = i.GetMethods(BindingFlags.Public | BindingFlags.Instance).First()
                let instance = Activator.CreateInstance(t)!
                select new KeyValuePair<Type, Func<object, object>>(
                    sourceType,
                    e => convertMethod.Invoke(instance, new[] {e})!);

        try
        {
            return c.ToDictionary(x => x.Key, x => x.Value);
        }
        catch (ArgumentException e)
        {
            throw new MultipleConvertersFoundException(e.Message, e);
        }
    }

    public virtual EventUpconverterWireup WithConvertersFrom(params Assembly[] assemblies)
    {
        _logger.LogDebug(Messages.EventUpconvertersLoadedFrom, string.Concat(", ", assemblies));
        _assembliesToScan.AddRange(assemblies);
        return this;
    }

    public virtual EventUpconverterWireup WithConvertersFromAssemblyContaining(params Type[] converters)
    {
        var assemblies = converters.Select(c => c.Assembly).Distinct();
        _logger.LogDebug(Messages.EventUpconvertersLoadedFrom, string.Concat(", ", assemblies));
        _assembliesToScan.AddRange(assemblies);
        return this;
    }

    public virtual EventUpconverterWireup AddConverter<TSource, TTarget>(
        IUpconvertEvents<TSource, TTarget> converter)
        where TSource : class where TTarget : class
    {
        if (converter == null)
        {
            throw new ArgumentNullException(nameof(converter));
        }

        _registered[typeof(TSource)] = @event => converter.Convert((TSource)@event);

        return this;
    }
}