// ReSharper disable once CheckNamespace
namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests
{
    using System;
    using OpenMedStack.NEventStore.Persistence;

    public abstract class PersistenceEngineFixtureBase : IDisposable
    {
        protected Func<int, IPersistStreams> CreatePersistence = null!;

        public void Initialize(int pageSize)
        {
            Persistence = CreatePersistence(pageSize);
            Persistence.Initialize();
        }

        public IPersistStreams Persistence { get; private set; } = null!;

        public void Dispose()
        {
            if (Persistence != null && !Persistence.IsDisposed)
            {
                Persistence.Drop();
                Persistence.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}
