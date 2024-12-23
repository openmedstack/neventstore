using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.Persistence.InMemory;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class InMemoryPersistenceEngine : IManagePersistence, ICommitEvents, IAccessSnapshots
{
    private readonly ILogger<InMemoryPersistenceEngine> _logger;
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();
    private int _checkpoint;

    public InMemoryPersistenceEngine(ILogger<InMemoryPersistenceEngine> logger)
    {
        _logger = logger;
    }

    private Bucket this[string TenantId]
    {
        get { return _buckets.GetOrAdd(TenantId, static (_, logger) => new Bucket(logger), _logger); }
    }

    public void Dispose()
    {
        IsDisposed = true;
        _logger.LogInformation(Resources.DisposingEngine);
        GC.SuppressFinalize(this);
    }

    public Task Initialize()
    {
        _logger.LogInformation(Resources.InitializingEngine);
        return Task.CompletedTask;
    }

    public IAsyncEnumerable<ICommit> Get(
        string tenantId,
        string streamId,
        int minRevision,
        int maxRevision,
        CancellationToken cancellationToken = default)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.GettingAllCommitsFromRevision, streamId, minRevision, maxRevision);
        var enumerable = this[tenantId].GetFrom(streamId, minRevision, maxRevision);
        return enumerable.ToAsyncEnumerable(cancellationToken);
    }

    public IAsyncEnumerable<ICommit> GetFrom(
        string TenantId,
        DateTimeOffset start,
        CancellationToken cancellationToken)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.GettingAllCommitsFromTime, TenantId, start);
        return this[TenantId].GetFrom(start).ToAsyncEnumerable(cancellationToken);
    }

    public IAsyncEnumerable<ICommit> GetFrom(
        string TenantId,
        long checkpointToken,
        CancellationToken cancellationToken)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.GettingAllCommitsFromBucketAndCheckpoint, TenantId, checkpointToken);
        return this[TenantId].GetFrom(checkpointToken).ToAsyncEnumerable(cancellationToken);
    }

    public IAsyncEnumerable<ICommit> GetFrom(
        long checkpointToken = 0L,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(Resources.GettingAllCommitsFromCheckpoint, checkpointToken);
        var items = _buckets.Values.SelectMany(b => b.GetCommits())
            .Where(c => c.CheckpointToken.CompareTo(checkpointToken) > 0)
            .OrderBy(c => c.CheckpointToken)
            .ToArray();
        return items.ToAsyncEnumerable(cancellationToken);
    }

    public IAsyncEnumerable<ICommit> GetFromTo(
        string TenantId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.GettingAllCommitsFromToTime, start, end);
        var bucket = this[TenantId];
        var fromTo = bucket.GetFromTo(start, end);
        return fromTo.ToAsyncEnumerable(cancellationToken);
    }

    public async Task<ICommit?> Commit(
        CommitAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        if (attempt.Events.Count == 0)
        {
            return null;
        }

        await Task.Yield();
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.AttemptingToCommit, attempt.CommitId, attempt.StreamId,
            attempt.CommitSequence);
        var bucket = this[attempt.TenantId];
        try
        {
            return bucket.Commit(attempt, Interlocked.Increment(ref _checkpoint));
        }
        catch (ConcurrencyException e)
        {
            if (DetectDuplicate(attempt))
            {
                _logger.LogError(e, "Duplicate commit detected");
                throw new DuplicateCommitException(e.Message, e);
            }

            _logger.LogError(e, "Concurrent commit detected. Retrying");
            throw;
        }
    }

    public IAsyncEnumerable<IStreamHead> GetStreamsToSnapshot(
        string TenantId,
        int maxThreshold,
        CancellationToken cancellationToken)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.GettingStreamsToSnapshot, TenantId, maxThreshold);
        return this[TenantId].GetStreamsToSnapshot(maxThreshold).ToAsyncEnumerable(cancellationToken);
    }

    public Task<ISnapshot?> GetSnapshot(
        string tenantId,
        string streamId,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.GettingSnapshotForStream, tenantId, streamId, maxRevision);
        return Task.FromResult(this[tenantId].GetSnapshot(streamId, maxRevision));
    }

    public Task<bool> AddSnapshot(ISnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.AddingSnapshot, snapshot.StreamId, snapshot.StreamRevision);
        return Task.FromResult(this[snapshot.TenantId].AddSnapshot(snapshot));
    }

    public Task<bool> Purge(string TenantId)
    {
        _buckets.TryRemove(TenantId, out _);
        return Task.FromResult(true);
    }

    public Task<bool> Drop()
    {
        _buckets.Clear();
        return Task.FromResult(true);
    }

    public Task<bool> DeleteStream(string TenantId, string streamId)
    {
        _logger.LogWarning(Resources.DeletingStream, streamId, TenantId);
        if (_buckets.TryGetValue(TenantId, out var bucket))
        {
            bucket.DeleteStream(streamId);
        }

        return Task.FromResult(true);
    }

    public bool IsDisposed { get; private set; }

    private bool DetectDuplicate(CommitAttempt attempt)
    {
        return this[attempt.TenantId].GetCommits()
            .Any(c => c.StreamId == attempt.StreamId && c.CommitId == attempt.CommitId);
    }

    private void ThrowWhenDisposed()
    {
        if (!IsDisposed)
        {
            return;
        }

        _logger.LogWarning(Resources.AlreadyDisposed);
        throw new ObjectDisposedException(Resources.AlreadyDisposed);
    }

    private class InMemoryCommit : Commit
    {
        public InMemoryCommit(
            string tenantId,
            string streamId,
            int streamRevision,
            Guid commitId,
            int commitSequence,
            DateTimeOffset commitStamp,
            long checkpointToken,
            IDictionary<string, object> headers,
            IEnumerable<EventMessage> events)
            : base(
                tenantId,
                streamId,
                streamRevision,
                commitId,
                commitSequence,
                commitStamp,
                checkpointToken,
                headers,
                events)
        {
        }
    }

    private class IdentityForConcurrencyConflictDetection
    {
        protected bool Equals(IdentityForConcurrencyConflictDetection other) =>
            string.Equals(_streamId, other._streamId)
         && string.Equals(_TenantId, other._TenantId)
         && _commitSequence == other._commitSequence;

        public override bool Equals(object? obj)
        {
            return obj is IdentityForConcurrencyConflictDetection other && Equals(other);
        }

        public override int GetHashCode() => HashCode.Combine(_streamId, _TenantId, _commitSequence);

        private readonly int _commitSequence;

        private readonly string _TenantId;

        private readonly string _streamId;

        public IdentityForConcurrencyConflictDetection(CommitAttempt commitAttempt)
        {
            _TenantId = commitAttempt.TenantId;
            _streamId = commitAttempt.StreamId;
            _commitSequence = commitAttempt.CommitSequence;
        }

        public IdentityForConcurrencyConflictDetection(Commit commit)
        {
            _TenantId = commit.TenantId;
            _streamId = commit.StreamId;
            _commitSequence = commit.CommitSequence;
        }
    }

    private class IdentityForDuplicationDetection
    {
        protected bool Equals(IdentityForDuplicationDetection other) =>
            string.Equals(_streamId, other._streamId)
         && string.Equals(_TenantId, other._TenantId)
         && _commitId.Equals(other._commitId);

        public override bool Equals(object? obj)
        {
            return obj is IdentityForDuplicationDetection other && Equals(other);
        }

        public override int GetHashCode() => HashCode.Combine(_streamId, _TenantId, _commitId);

        private readonly Guid _commitId;

        private readonly string _TenantId;

        private readonly string _streamId;

        public IdentityForDuplicationDetection(CommitAttempt commitAttempt)
        {
            _TenantId = commitAttempt.TenantId;
            _streamId = commitAttempt.StreamId;
            _commitId = commitAttempt.CommitId;
        }

        public IdentityForDuplicationDetection(Commit commit)
        {
            _TenantId = commit.TenantId;
            _streamId = commit.StreamId;
            _commitId = commit.CommitId;
        }
    }

    private class Bucket
    {
        private readonly ILogger _logger;
        private readonly IList<InMemoryCommit> _commits = new List<InMemoryCommit>();

        private readonly ICollection<IdentityForDuplicationDetection> _potentialDuplicates =
            new HashSet<IdentityForDuplicationDetection>();

        private readonly ICollection<IdentityForConcurrencyConflictDetection> _potentialConflicts =
            new HashSet<IdentityForConcurrencyConflictDetection>();

        public Bucket(ILogger logger)
        {
            _logger = logger;
        }

        public IEnumerable<InMemoryCommit> GetCommits()
        {
            lock (_commits)
            {
                return _commits.ToArray();
            }
        }

        private readonly ICollection<IStreamHead> _heads = new LinkedList<IStreamHead>();
        private readonly ICollection<ISnapshot> _snapshots = new LinkedList<ISnapshot>();
        private readonly IDictionary<Guid, DateTimeOffset> _stamps = new Dictionary<Guid, DateTimeOffset>();

        public IEnumerable<ICommit> GetFrom(string streamId, int minRevision, int maxRevision)
        {
            lock (_commits)
            {
                return _commits
                    .Where(
                        x => x.StreamId == streamId
                         && x.StreamRevision >= minRevision
                         && (x.StreamRevision - x.Events.Count + 1) <= maxRevision)
                    .OrderBy(c => c.CommitSequence)
                    .ToArray();
            }
        }

        public IEnumerable<ICommit> GetFrom(DateTimeOffset start)
        {
            lock (_commits)
            {
                var commitId = _stamps.Where(x => x.Value >= start).Select(x => x.Key).FirstOrDefault();
                if (commitId == Guid.Empty)
                {
                    return [];
                }

                var startingCommit = _commits.FirstOrDefault(x => x.CommitId == commitId);
                return _commits.Skip(startingCommit is null ? 0 : _commits.IndexOf(startingCommit));
            }
        }

        public IEnumerable<ICommit> GetFrom(long checkpoint)
        {
            lock (_commits)
            {
                var startingCommit = _commits.FirstOrDefault(x => x.CheckpointToken.CompareTo(checkpoint) == 0);
                var skip = startingCommit == null ? -1 : _commits.IndexOf(startingCommit);
                return _commits.Skip(skip + 1 /* GetFrom => after the checkpoint*/);
            }
        }

        public IEnumerable<ICommit> GetFromTo(DateTimeOffset start, DateTimeOffset end)
        {
            IEnumerable<Guid> selectedCommitIds =
                _stamps.Where(x => x.Value >= start && x.Value <= end).Select(x => x.Key).ToArray();
            var firstCommitId = selectedCommitIds.FirstOrDefault();
            var lastCommitId = selectedCommitIds.LastOrDefault();
            if (firstCommitId == Guid.Empty && lastCommitId == Guid.Empty)
            {
                return [];
            }

            lock (_commits)
            {
                var startingCommit = _commits.FirstOrDefault(x => x.CommitId == firstCommitId);
                var endingCommit = _commits.FirstOrDefault(x => x.CommitId == lastCommitId);
                var startingCommitIndex = (startingCommit == null) ? 0 : _commits.IndexOf(startingCommit);
                var endingCommitIndex = (endingCommit == null) ? _commits.Count - 1 : _commits.IndexOf(endingCommit);
                var numberToTake = endingCommitIndex - startingCommitIndex + 1;

                return _commits.Skip(startingCommitIndex).Take(numberToTake);
            }
        }

        public ICommit Commit(CommitAttempt attempt, long checkpoint)
        {
            DetectDuplicate(attempt);
            var commit = new InMemoryCommit(
                attempt.TenantId,
                attempt.StreamId,
                attempt.StreamRevision,
                attempt.CommitId,
                attempt.CommitSequence,
                attempt.CommitStamp,
                checkpoint,
                attempt.Headers,
                attempt.Events);
            if (_potentialConflicts.Contains(new IdentityForConcurrencyConflictDetection(commit)))
            {
                throw new ConcurrencyException();
            }

            lock (_commits)
            {
                _stamps[commit.CommitId] = commit.CommitStamp;
                _commits.Add(commit);
            }

            _potentialDuplicates.Add(new IdentityForDuplicationDetection(commit));
            _potentialConflicts.Add(new IdentityForConcurrencyConflictDetection(commit));
            lock (_heads)
            {
                var head = _heads.FirstOrDefault(x => x.StreamId == commit.StreamId);
                if (head != null)
                {
                    _heads.Remove(head);
                }

                _logger.LogDebug(Resources.UpdatingStreamHead, commit.StreamId);
                var snapshotRevision = head?.SnapshotRevision ?? 0;
                _heads.Add(
                    new StreamHead(commit.TenantId, commit.StreamId, commit.StreamRevision, snapshotRevision));
            }

            return commit;
        }

        private void DetectDuplicate(CommitAttempt attempt)
        {
            if (_potentialDuplicates.Contains(new IdentityForDuplicationDetection(attempt)))
            {
                throw new DuplicateCommitException();
            }
        }

        public IEnumerable<IStreamHead> GetStreamsToSnapshot(int maxThreshold)
        {
            lock (_commits)
            {
                return _heads.Where(x => x.HeadRevision > x.SnapshotRevision + maxThreshold)
                    .Select(
                        stream => new StreamHead(
                            stream.TenantId,
                            stream.StreamId,
                            stream.HeadRevision,
                            stream.SnapshotRevision));
            }
        }

        public ISnapshot? GetSnapshot(string streamId, int maxRevision)
        {
            lock (_commits)
            {
                return _snapshots.Where(x => x.StreamId == streamId && x.StreamRevision <= maxRevision)
                    .MaxBy(x => x.StreamRevision);
            }
        }

        public bool AddSnapshot(ISnapshot snapshot)
        {
            lock (_heads)
            {
                var currentHead = _heads.FirstOrDefault(h => h.StreamId == snapshot.StreamId);
                if (currentHead == null)
                {
                    return false;
                }

                _snapshots.Add(snapshot);
                _heads.Remove(currentHead);
                _heads.Add(
                    new StreamHead(
                        currentHead.TenantId,
                        currentHead.StreamId,
                        currentHead.HeadRevision,
                        snapshot.StreamRevision));
            }

            return true;
        }

        public void Purge()
        {
            lock (_commits)
            {
                _commits.Clear();
            }

            _snapshots.Clear();
            lock (_heads)
            {
                _heads.Clear();
            }

            _potentialConflicts.Clear();
            _potentialDuplicates.Clear();
        }

        public void DeleteStream(string streamId)
        {
            lock (_commits)
            {
                var commits = _commits.Where(c => c.StreamId == streamId).ToArray();
                foreach (var commit in commits)
                {
                    _commits.Remove(commit);
                }
            }

            var snapshots = _snapshots.Where(s => s.StreamId == streamId).ToArray();
            foreach (var snapshot in snapshots)
            {
                _snapshots.Remove(snapshot);
            }

            lock (_heads)
            {
                var streamHead = _heads.SingleOrDefault(s => s.StreamId == streamId);
                if (streamHead != null)
                {
                    _heads.Remove(streamHead);
                }
            }
        }
    }
}
