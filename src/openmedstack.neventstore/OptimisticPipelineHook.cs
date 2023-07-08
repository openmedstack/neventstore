using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Persistence;

namespace OpenMedStack.NEventStore
{
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     Tracks the heads of streams to reduce latency by avoiding round trips to storage.
    /// </summary>
    public class OptimisticPipelineHook : PipelineHookBase
    {
        private const int MaxStreamsToTrack = 100;
        private readonly ILogger _logger; private readonly Dictionary<HeadKey, ICommit> _heads = new(); //TODO use concurrent collections
        private readonly LinkedList<HeadKey> _maxItemsToTrack = new();
        private readonly int _maxStreamsToTrack;

        public OptimisticPipelineHook(ILogger logger)
            : this(MaxStreamsToTrack, logger)
        { }

        public OptimisticPipelineHook(int maxStreamsToTrack, ILogger logger)
        {
            _logger = logger;
            _logger.LogDebug(Resources.TrackingStreams, maxStreamsToTrack);
            _maxStreamsToTrack = maxStreamsToTrack;
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public override Task<ICommit> Select(ICommit committed)
        {
            Track(committed);
            return Task.FromResult(committed);
        }

        public override Task<bool> PreCommit(CommitAttempt attempt)
        {
            _logger.LogTrace(Resources.OptimisticConcurrencyCheck, attempt.StreamId);

            var head = GetStreamHead(GetHeadKey(attempt));
            if (head == null)
            {
                return Task.FromResult(true);
            }

            if (head.CommitSequence >= attempt.CommitSequence)
            {
                throw new ConcurrencyException(string.Format(
                    Messages.ConcurrencyExceptionCommitSequence,
                    head.CommitSequence,
                    attempt.CommitSequence,
                    attempt.StreamId,
                    attempt.StreamRevision,
                    attempt.Events.Count
                ));
            }

            if (head.StreamRevision >= attempt.StreamRevision)
            {
                throw new ConcurrencyException(string.Format(
                    Messages.ConcurrencyExceptionStreamRevision,
                    head.StreamRevision,
                    attempt.StreamRevision,
                    attempt.StreamId,
                    attempt.StreamRevision,
                    attempt.Events.Count
                ));
            }

            if (head.CommitSequence < attempt.CommitSequence - 1)
            {
                throw new StorageException(string.Format(
                     Messages.StorageExceptionCommitSequence,
                     head.CommitSequence,
                     attempt.CommitSequence,
                     attempt.StreamId,
                     attempt.StreamRevision,
                     attempt.Events.Count
                 )); // beyond the end of the stream
            }

            if (head.StreamRevision < attempt.StreamRevision - attempt.Events.Count)
            {
                throw new StorageException(string.Format(
                     Messages.StorageExceptionEndOfStream,
                     head.StreamRevision,
                     attempt.StreamRevision,
                     attempt.Events.Count,
                     attempt.StreamId,
                     attempt.StreamRevision
                 )); // beyond the end of the stream
            }

            _logger.LogTrace(Resources.NoConflicts, attempt.StreamId);
            return Task.FromResult(true);
        }

        public override Task PostCommit(ICommit committed)
        {
            Track(committed);

            return Task.CompletedTask;
        }

        public override Task OnPurge(string? bucketId)
        {
            lock (_maxItemsToTrack)
            {
                if (bucketId == null)
                {
                    _heads.Clear();
                    _maxItemsToTrack.Clear();

                    return Task.CompletedTask;
                }
                var headsInBucket = _heads.Keys.Where(k => k.BucketId == bucketId).ToArray();
                foreach (var head in headsInBucket)
                {
                    RemoveHead(head);
                }
            }

            return Task.CompletedTask;
        }

        public override Task OnDeleteStream(string bucketId, string streamId)
        {
            lock (_maxItemsToTrack)
            {
                RemoveHead(new HeadKey(bucketId, streamId));
            }

            return Task.CompletedTask;
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (_maxItemsToTrack)
            {
                _heads.Clear();
                _maxItemsToTrack.Clear();
            }
        }

        public virtual void Track(ICommit committed)
        {
            lock (_maxItemsToTrack)
            {
                UpdateStreamHead(committed);
                TrackUpToCapacity(committed);
            }
        }

        private void UpdateStreamHead(ICommit committed)
        {
            var headKey = GetHeadKey(committed);
            var head = GetStreamHead(headKey);
            if (AlreadyTracked(head))
            {
                _maxItemsToTrack.Remove(headKey);
            }

            head ??= committed;
            head = head.StreamRevision > committed.StreamRevision ? head : committed;

            _heads[headKey] = head;
        }

        private void RemoveHead(HeadKey head)
        {
            _heads.Remove(head);
            var node = _maxItemsToTrack.Find(head); // There should only be ever one or none
            if (node != null)
            {
                _maxItemsToTrack.Remove(node);
            }
        }

        private static bool AlreadyTracked(ICommit? head) => head != null;

        private void TrackUpToCapacity(ICommit committed)
        {
            _logger.LogTrace(Resources.TrackingCommit, committed.CommitSequence, committed.StreamId);
            _maxItemsToTrack.AddFirst(GetHeadKey(committed));
            if (_maxItemsToTrack.Count <= _maxStreamsToTrack)
            {
                return;
            }

            var expired = _maxItemsToTrack.Last!.Value;
            _logger.LogTrace(Resources.NoLongerTrackingStream, expired);

            _heads.Remove(expired);
            _maxItemsToTrack.RemoveLast();
        }

        public virtual bool Contains(ICommit attempt) => GetStreamHead(GetHeadKey(attempt)) != null;

        private ICommit? GetStreamHead(HeadKey headKey)
        {
            lock (_maxItemsToTrack)
            {
                _heads.TryGetValue(headKey, out var head);
                return head;
            }
        }

        private static HeadKey GetHeadKey(ICommit commit) => new(commit.BucketId, commit.StreamId);

        private static HeadKey GetHeadKey(CommitAttempt commitAttempt) => new(commitAttempt.BucketId, commitAttempt.StreamId);

        private sealed class HeadKey : IEquatable<HeadKey>
        {
            public HeadKey(string bucketId, string streamId)
            {
                BucketId = bucketId;
                StreamId = streamId;
            }

            public string BucketId { get; }

            public string StreamId { get; }

            public bool Equals(HeadKey? other)
            {
                if (other is null)
                {
                    return false;
                }
                if (ReferenceEquals(this, other))
                {
                    return true;
                }
                return string.Equals(BucketId, other.BucketId) && string.Equals(StreamId, other.StreamId);
            }

            public override bool Equals(object? obj)
            {
                if (obj is null)
                {
                    return false;
                }
                if (ReferenceEquals(this, obj))
                {
                    return true;
                }
                return obj is HeadKey key && Equals(key);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (BucketId.GetHashCode() * 397) ^ StreamId.GetHashCode();
                }
            }
        }
    }
}
