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

    public async Task<ICommit?> Commit(IEventStream eventStream, Guid? commitId, CancellationToken cancellationToken)
    {
        if (eventStream.UncommittedEvents.Count == 0)
        {
            return null;
        }

        commitId ??= Guid.NewGuid();

        foreach (var hook in _pipelineHooks)
        {
            _logger.LogTrace(Resources.InvokingPreCommitHooks, commitId, hook.GetType());
            if (await hook.PreCommit(eventStream).ConfigureAwait(false))
            {
                continue;
            }

            _logger.LogInformation(Resources.CommitRejectedByPipelineHook, hook.GetType(), commitId);
            return null;
        }

        _logger.LogTrace(Resources.CommittingAttempt, commitId, eventStream.UncommittedEvents.Count);
        try
        {
            var commit = await _persistence.Commit(eventStream, commitId, cancellationToken).ConfigureAwait(false);

            if (commit == null)
            {
                return commit;
            }

            foreach (var hook in _pipelineHooks)
            {
                _logger.LogTrace(Resources.InvokingPostCommitPipelineHooks, commitId, hook.GetType());
                await hook.PostCommit(commit).ConfigureAwait(false);
            }

            return commit;
        }
        catch (ConcurrencyException)
        {
            var currentRevision = eventStream.StreamRevision - eventStream.UncommittedEvents.Count;
            await eventStream.Update(this, cancellationToken).ConfigureAwait(false);
            if (eventStream.StreamRevision == currentRevision)
            {
                throw;
            }

            return await Commit(eventStream, commitId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error committing event stream");
            throw;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual async Task<IEventStream> CreateStream(
        string bucketId,
        string streamId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(Resources.CreatingStream, streamId, bucketId);
        return await OptimisticEventStream.Create(bucketId, streamId, Advanced, 0, int.MaxValue,
            _loggerFactory.CreateLogger<OptimisticEventStream>(), cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<IEventStream> OpenStream(
        string bucketId,
        string streamId,
        int minRevision,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        maxRevision = maxRevision <= 0 ? int.MaxValue : maxRevision;

        _logger.LogTrace(Resources.OpeningStreamAtRevision, streamId, bucketId, minRevision, maxRevision);
        return await OptimisticEventStream.Create(bucketId, streamId, Advanced, minRevision, maxRevision,
            _loggerFactory.CreateLogger<OptimisticEventStream>(), cancellationToken).ConfigureAwait(false);
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

        return await OptimisticEventStream.Create(snapshot, Advanced, maxRevision,
            _loggerFactory.CreateLogger<OptimisticEventStream>(), cancellationToken).ConfigureAwait(false);
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
