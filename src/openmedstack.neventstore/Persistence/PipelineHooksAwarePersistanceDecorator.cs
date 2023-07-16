using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenMedStack.NEventStore.Persistence
{
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    public class PipelineHooksAwarePersistenceDecorator : IPersistStreams
    {
        private readonly ILogger _logger;
        private readonly IPersistStreams _original;
        private readonly IEnumerable<IPipelineHook> _pipelineHooks;

        public PipelineHooksAwarePersistenceDecorator(
            IPersistStreams original,
            IEnumerable<IPipelineHook> pipelineHooks,
            ILogger logger)
        {
            _original = original ?? throw new ArgumentNullException(nameof(original));
            _pipelineHooks = pipelineHooks ?? throw new ArgumentNullException(nameof(pipelineHooks));
            _logger = logger;
        }

        public void Dispose()
        {
            _original.Dispose();
            GC.SuppressFinalize(this);
        }

        public IAsyncEnumerable<ICommit> GetFrom(
            string bucketId,
            string streamId,
            int minRevision,
            int maxRevision,
            CancellationToken cancellationToken = default)
        {
            var configureAwait = _original.GetFrom(bucketId, streamId, minRevision, maxRevision, cancellationToken);
            return ExecuteHooks(configureAwait, cancellationToken);
        }

        public Task<ICommit?> Commit(CommitAttempt attempt) => _original.Commit(attempt);

        public Task<ISnapshot?> GetSnapshot(
            string bucketId,
            string streamId,
            int maxRevision,
            CancellationToken cancellationToken) =>
            _original.GetSnapshot(bucketId, streamId, maxRevision, cancellationToken);

        public Task<bool> AddSnapshot(ISnapshot snapshot) => _original.AddSnapshot(snapshot);

        public IAsyncEnumerable<IStreamHead> GetStreamsToSnapshot(
            string bucketId,
            int maxThreshold,
            CancellationToken cancellationToken) =>
            _original.GetStreamsToSnapshot(bucketId, maxThreshold, cancellationToken);

        public Task Initialize() => _original.Initialize();

        public IAsyncEnumerable<ICommit> GetFrom(
            string bucketId,
            DateTimeOffset start,
            CancellationToken cancellationToken) =>
            ExecuteHooks(_original.GetFrom(bucketId, start, cancellationToken), cancellationToken);

        public IAsyncEnumerable<ICommit> GetFrom(
            string bucketId,
            long checkpointToken,
            CancellationToken cancellationToken)
        {
            var asyncEnumerable = _original.GetFrom(bucketId, checkpointToken, cancellationToken);
            return ExecuteHooks(asyncEnumerable, cancellationToken);
        }

        public IAsyncEnumerable<ICommit> GetFromTo(
            string bucketId,
            DateTimeOffset start,
            DateTimeOffset end,
            CancellationToken cancellationToken) =>
            ExecuteHooks(_original.GetFromTo(bucketId, start, end, cancellationToken), cancellationToken);

        public async Task<bool> Purge()
        {
            var result = await _original.Purge().ConfigureAwait(false);
            foreach (var pipelineHook in _pipelineHooks)
            {
                pipelineHook.OnPurge();
            }

            return result;
        }

        public async Task<bool> Purge(string bucketId)
        {
            var result = await _original.Purge(bucketId).ConfigureAwait(false);
            foreach (var pipelineHook in _pipelineHooks)
            {
                await pipelineHook.OnPurge(bucketId).ConfigureAwait(false);
            }

            return result;
        }

        public Task<bool> Drop() => _original.Drop();

        public async Task<bool> DeleteStream(string bucketId, string streamId)
        {
            var result = await _original.DeleteStream(bucketId, streamId).ConfigureAwait(false);
            foreach (var pipelineHook in _pipelineHooks)
            {
                await pipelineHook.OnDeleteStream(bucketId, streamId).ConfigureAwait(false);
            }

            return result;
        }

        public bool IsDisposed => _original.IsDisposed;

        private async IAsyncEnumerable<ICommit> ExecuteHooks(
            IAsyncEnumerable<ICommit> commits,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var commit in commits.WithCancellation(cancellationToken))
            {
                var filtered = commit;
                foreach (var hook in _pipelineHooks.Where(x => (filtered = x.Select(filtered).Result) == null))
                {
                    _logger.LogInformation(Resources.PipelineHookSkippedCommit, hook.GetType(), commit.CommitId);
                    break;
                }

                if (filtered == null)
                {
                    _logger.LogInformation(Resources.PipelineHookFilteredCommit);
                }
                else
                {
                    yield return filtered;
                }
            }
        }
    }
}
