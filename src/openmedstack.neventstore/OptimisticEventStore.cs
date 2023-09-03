using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Persistence;

namespace OpenMedStack.NEventStore;

using System.Threading;
using Microsoft.Extensions.Logging;

public class OptimisticEventStore : IStoreEvents, ICommitEvents
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<OptimisticEventStore> _logger;
    private readonly IPersistStreams _persistence;
    private readonly IEnumerable<IPipelineHook> _pipelineHooks;

    public OptimisticEventStore(
        IPersistStreams persistence,
        IEnumerable<IPipelineHook> pipelineHooks,
        ILoggerFactory loggerFactory)
    {
        if (persistence == null)
        {
            throw new ArgumentNullException(nameof(persistence));
        }

        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<OptimisticEventStore>();
        _pipelineHooks = pipelineHooks;
        _persistence = new PipelineHooksAwarePersistenceDecorator(persistence, _pipelineHooks, _logger);
    }

    public virtual IAsyncEnumerable<ICommit> GetFrom(
        string bucketId,
        string streamId,
        int minRevision,
        int maxRevision,
        CancellationToken cancellationToken) =>
        _persistence.GetFrom(bucketId, streamId, minRevision, maxRevision, cancellationToken);

    public virtual async Task<ICommit?> Commit(CommitAttempt attempt)
    {
        Guard.NotNull(nameof(attempt), attempt);
        foreach (var hook in _pipelineHooks)
        {
            _logger.LogTrace(Resources.InvokingPreCommitHooks, attempt.CommitId, hook.GetType());
            if (await hook.PreCommit(attempt).ConfigureAwait(false))
            {
                continue;
            }

            _logger.LogInformation(Resources.CommitRejectedByPipelineHook, hook.GetType(), attempt.CommitId);
            return null;
        }

        _logger.LogTrace(Resources.CommittingAttempt, attempt.CommitId, attempt.Events.Count);
        var commit = await _persistence.Commit(attempt).ConfigureAwait(false);

        if (commit != null)
        {
            foreach (var hook in _pipelineHooks)
            {
                _logger.LogTrace(Resources.InvokingPostCommitPipelineHooks, attempt.CommitId, hook.GetType());
                await hook.PostCommit(commit).ConfigureAwait(false);
            }
        }

        return commit;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual async Task<IEventStream> CreateStream(string bucketId, string streamId)
    {
        _logger.LogDebug(Resources.CreatingStream, streamId, bucketId);
        return await OptimisticEventStream
            .Create(bucketId, streamId, this, _loggerFactory.CreateLogger<OptimisticEventStream>())
            .ConfigureAwait(false);
    }

    public virtual async Task<IEventStream> OpenStream(
        string bucketId,
        string streamId,
        int minRevision,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        if (streamId == null)
        {
            throw new ArgumentNullException();
        }

        maxRevision = maxRevision <= 0 ? int.MaxValue : maxRevision;

        _logger.LogTrace(Resources.OpeningStreamAtRevision, streamId, bucketId, minRevision, maxRevision);
        return await OptimisticEventStream
            .Create(bucketId, streamId, this, minRevision, maxRevision,
                _loggerFactory.CreateLogger<OptimisticEventStream>(), cancellationToken)
            .ConfigureAwait(false);
    }

    public virtual async Task<IEventStream> OpenStream(
        ISnapshot snapshot,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        _logger.LogTrace(
            Resources.OpeningStreamWithSnapshot,
            snapshot.StreamId,
            snapshot.StreamRevision,
            maxRevision);
        maxRevision = maxRevision <= 0 ? int.MaxValue : maxRevision;
        return await OptimisticEventStream.Create(snapshot, this, maxRevision,
                _loggerFactory.CreateLogger<OptimisticEventStream>(), cancellationToken)
            .ConfigureAwait(false);
    }

    public virtual IPersistStreams Advanced => _persistence;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _logger.LogInformation(Resources.ShuttingDownStore);
        _persistence.Dispose();
        foreach (var hook in _pipelineHooks)
        {
            hook.Dispose();
        }
    }
}
