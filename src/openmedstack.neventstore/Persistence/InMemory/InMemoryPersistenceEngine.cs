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

public class InMemoryPersistenceEngine : IPersistStreams
{
    private readonly ILogger<InMemoryPersistenceEngine> _logger;
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();
    private int _checkpoint;

    public InMemoryPersistenceEngine(ILogger<InMemoryPersistenceEngine> logger)
    {
        _logger = logger;
    }

    private Bucket this[string bucketId]
    {
        get { return _buckets.GetOrAdd(bucketId, static (_, logger) => new Bucket(logger), _logger); }
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

    public IAsyncEnumerable<ICommit> GetFrom(
        string bucketId,
        string streamId,
        int minRevision,
        int maxRevision,
        CancellationToken cancellationToken = default)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.GettingAllCommitsFromRevision, streamId, minRevision, maxRevision);
        var enumerable = this[bucketId].GetFrom(streamId, minRevision, maxRevision);
        return enumerable.ToAsyncEnumerable(cancellationToken);
    }

    public IAsyncEnumerable<ICommit> GetFrom(
        string bucketId,
        DateTimeOffset start,
        CancellationToken cancellationToken)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.GettingAllCommitsFromTime, bucketId, start);
        return this[bucketId].GetFrom(start).ToAsyncEnumerable(cancellationToken);
    }

    public IAsyncEnumerable<ICommit> GetFrom(
        string bucketId,
        long checkpointToken,
        CancellationToken cancellationToken)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.GettingAllCommitsFromBucketAndCheckpoint, bucketId, checkpointToken);
        return this[bucketId].GetFrom(checkpointToken).ToAsyncEnumerable(cancellationToken);
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
        string bucketId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.GettingAllCommitsFromToTime, start, end);
        var bucket = this[bucketId];
        var fromTo = bucket.GetFromTo(start, end);
        return fromTo.ToAsyncEnumerable(cancellationToken);
    }

    public Task<ICommit?> Commit(
        IEventStream eventStream,
        Guid? commitId = null,
        CancellationToken cancellationToken = default)
    {
        if (eventStream.UncommittedEvents.Count == 0)
        {
            return Task.FromResult<ICommit?>(null);
        }

        ThrowWhenDisposed();
        _logger.LogDebug(Resources.AttemptingToCommit, commitId, eventStream.StreamId, eventStream.CommitSequence);
        var attempt = CommitAttempt.FromStream(eventStream, commitId ?? Guid.NewGuid());
        var bucket = this[attempt.BucketId];
        var commit = bucket.Commit(attempt, Interlocked.Increment(ref _checkpoint));
        return Task.FromResult<ICommit?>(commit);
    }

    public IAsyncEnumerable<IStreamHead> GetStreamsToSnapshot(
        string bucketId,
        int maxThreshold,
        CancellationToken cancellationToken)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.GettingStreamsToSnapshot, bucketId, maxThreshold);
        return this[bucketId].GetStreamsToSnapshot(maxThreshold).ToAsyncEnumerable(cancellationToken);
    }

    public Task<ISnapshot?> GetSnapshot(
        string bucketId,
        string streamId,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.GettingSnapshotForStream, bucketId, streamId, maxRevision);
        return Task.FromResult(this[bucketId].GetSnapshot(streamId, maxRevision));
    }

    public Task<bool> AddSnapshot(ISnapshot snapshot)
    {
        ThrowWhenDisposed();
        _logger.LogDebug(Resources.AddingSnapshot, snapshot.StreamId, snapshot.StreamRevision);
        return Task.FromResult(this[snapshot.BucketId].AddSnapshot(snapshot));
    }

    public Task<bool> Purge()
    {
        ThrowWhenDisposed();
        _logger.LogWarning(Resources.PurgingStore);
        foreach (var bucket in _buckets.Values)
        {
            bucket.Purge();
        }

        return Task.FromResult(true);
    }

    public Task<bool> Purge(string bucketId)
    {
        _buckets.TryRemove(bucketId, out var _);
        return Task.FromResult(true);
    }

    public Task<bool> Drop()
    {
        _buckets.Clear();
        return Task.FromResult(true);
    }

    public Task<bool> DeleteStream(string bucketId, string streamId)
    {
        _logger.LogWarning(Resources.DeletingStream, streamId, bucketId);
        if (_buckets.TryGetValue(bucketId, out var bucket))
        {
            bucket.DeleteStream(streamId);
        }

        return Task.FromResult(true);
    }

    public bool IsDisposed { get; private set; }

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
            string bucketId,
            string streamId,
            int streamRevision,
            Guid commitId,
            int commitSequence,
            DateTimeOffset commitStamp,
            long checkpointToken,
            IDictionary<string, object> headers,
            IEnumerable<EventMessage> events)
            : base(
                bucketId,
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
         && string.Equals(_bucketId, other._bucketId)
         && _commitSequence == other._commitSequence;

        public override bool Equals(object? obj)
        {
            return obj is IdentityForConcurrencyConflictDetection other && Equals(other);
        }

        public override int GetHashCode() => HashCode.Combine(_streamId, _bucketId, _commitSequence);

        private readonly int _commitSequence;

        private readonly string _bucketId;

        private readonly string _streamId;

        public IdentityForConcurrencyConflictDetection(CommitAttempt commitAttempt)
        {
            _bucketId = commitAttempt.BucketId;
            _streamId = commitAttempt.StreamId;
            _commitSequence = commitAttempt.CommitSequence;
        }

        public IdentityForConcurrencyConflictDetection(Commit commit)
        {
            _bucketId = commit.BucketId;
            _streamId = commit.StreamId;
            _commitSequence = commit.CommitSequence;
        }
    }

    private class IdentityForDuplicationDetection
    {
        protected bool Equals(IdentityForDuplicationDetection other) =>
            string.Equals(_streamId, other._streamId)
         && string.Equals(_bucketId, other._bucketId)
         && _commitId.Equals(other._commitId);

        public override bool Equals(object? obj)
        {
            return obj is IdentityForDuplicationDetection other && Equals(other);
        }

        public override int GetHashCode() => HashCode.Combine(_streamId, _bucketId, _commitId);

        private readonly Guid _commitId;

        private readonly string _bucketId;

        private readonly string _streamId;

        public IdentityForDuplicationDetection(CommitAttempt commitAttempt)
        {
            _bucketId = commitAttempt.BucketId;
            _streamId = commitAttempt.StreamId;
            _commitId = commitAttempt.CommitId;
        }

        public IdentityForDuplicationDetection(Commit commit)
        {
            _bucketId = commit.BucketId;
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
                    return Enumerable.Empty<ICommit>();
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
                return Enumerable.Empty<ICommit>();
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
                attempt.BucketId,
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

            _stamps[commit.CommitId] = commit.CommitStamp;
            lock (_commits)
            {
                _commits.Add(commit);
            }

            _potentialDuplicates.Add(new IdentityForDuplicationDetection(commit));
            _potentialConflicts.Add(new IdentityForConcurrencyConflictDetection(commit));
            var head = _heads.FirstOrDefault(x => x.StreamId == commit.StreamId);
            if (head != null)
            {
                _heads.Remove(head);
            }

            _logger.LogDebug(Resources.UpdatingStreamHead, commit.StreamId);
            var snapshotRevision = head?.SnapshotRevision ?? 0;
            _heads.Add(
                new StreamHead(commit.BucketId, commit.StreamId, commit.StreamRevision, snapshotRevision));
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
                return _heads.Where(x => x.HeadRevision >= x.SnapshotRevision + maxThreshold)
                    .Select(
                        stream => new StreamHead(
                            stream.BucketId,
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
                        currentHead.BucketId,
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
