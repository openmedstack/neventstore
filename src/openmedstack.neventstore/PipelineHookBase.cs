using System.Threading.Tasks;

namespace OpenMedStack.NEventStore
{
    using System;

    public abstract class PipelineHookBase : IPipelineHook
    {
        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public virtual Task<ICommit> Select(ICommit committed) => Task.FromResult(committed);

        public virtual Task<bool> PreCommit(CommitAttempt attempt) => Task.FromResult(true);

        public virtual Task PostCommit(ICommit committed) => Task.CompletedTask;

        public virtual Task OnPurge(string? bucketId) => Task.CompletedTask;

        public virtual Task OnDeleteStream(string bucketId, string streamId) => Task.CompletedTask;
    }
}