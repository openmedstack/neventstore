namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.BDD
{
    using System;
    using System.Threading.Tasks;

    //[RunWith(typeof(SpecificationBaseRunner))]
    public abstract class SpecificationBase : IDisposable
    {
        public void Dispose()
        {
            OnFinish();
            GC.SuppressFinalize(this);
        }

        protected virtual Task Because() => Task.CompletedTask;

        protected virtual void Cleanup()
        { }

        protected virtual Task Context() => Task.CompletedTask;

        public void OnFinish()
        {
            Cleanup();
        }

        public async Task OnStart()
        {
            await Context().ConfigureAwait(false);
            await Because().ConfigureAwait(false);
        }
    }
}
