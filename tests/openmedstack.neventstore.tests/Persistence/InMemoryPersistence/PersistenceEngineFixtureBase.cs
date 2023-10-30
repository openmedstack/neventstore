// ReSharper disable once CheckNamespace

using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests;

using System;

public abstract class PersistenceEngineFixtureBase : IDisposable
{
    protected Func<int, (IManagePersistence, ICommitEvents, IAccessSnapshots)> CreatePersistence = null!;

    public void Initialize(int pageSize)
    {
        var (managePersistence, commitEvents, accessSnapshots) = CreatePersistence(pageSize);
        Persistence = commitEvents;
        Snapshots = accessSnapshots;
        PersistenceManagement = managePersistence;
        PersistenceManagement.Initialize().Wait();
    }

    public ICommitEvents Persistence { get; private set; } = null!;
    public IAccessSnapshots Snapshots { get; private set; } = null!;
    public IManagePersistence PersistenceManagement { get; private set; } = null!;

    public void Dispose()
    {
        PersistenceManagement.Drop();
        GC.SuppressFinalize(this);
    }
}
