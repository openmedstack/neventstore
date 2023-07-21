namespace OpenMedStack.NEventStore.Persistence.Sql;

using System;
using System.Threading;
using Microsoft.Extensions.Logging;

// HttpContext.Current is not a good idea, it's not supported in netstandard, possible alternatives (that requires some setup):
// https://www.strathweb.com/2016/12/accessing-httpcontext-outside-of-framework-components-in-asp-net-core/

public class ThreadScope<T> : IDisposable
    where T : class
{
    private readonly ILogger _logger;
    private readonly bool _rootScope;
    private readonly string _threadKey;
    private bool _disposed;

    public ThreadScope(string key, Func<T> factory, ILogger logger)
    {
        _logger = logger;
        _threadKey = typeof(ThreadScope<T>).Name + $":[{key ?? string.Empty}]";

        var parent = Load();
        _rootScope = parent == null;
        _logger.LogDebug(PersistenceMessages.OpeningThreadScope, _threadKey, _rootScope);

        Current = parent ?? factory();

        if (Current == null)
        {
            throw new ArgumentException(PersistenceMessages.BadFactoryResult, nameof(factory));
        }

        if (_rootScope)
        {
            Store(Current);
        }
    }

    public T Current { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || _disposed)
        {
            return;
        }

        _logger.LogDebug(PersistenceMessages.DisposingThreadScope, _rootScope);
        _disposed = true;
        if (!_rootScope)
        {
            return;
        }

        _logger.LogTrace(PersistenceMessages.CleaningRootThreadScope);
        Store(null);

        if (Current is not IDisposable resource)
        {
            return;
        }

        _logger.LogTrace(PersistenceMessages.DisposingRootThreadScopeResources);
        resource.Dispose();
    }

    private T? Load() => (Thread.GetData(Thread.GetNamedDataSlot(_threadKey)) as T)!;

    private void Store(T? value)
    {
        Thread.SetData(Thread.GetNamedDataSlot(_threadKey), value);
    }
}