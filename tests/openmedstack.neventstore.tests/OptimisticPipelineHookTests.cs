using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NEventStore;
using NEventStore.Persistence;
using NEventStore.Persistence.AcceptanceTests;
using NEventStore.Persistence.AcceptanceTests.BDD;
using Xunit;

public class OptimisticPipelineHookTests
{
    public class WhenCommittingWithASequenceBeyondTheKnownEndOfAStream : UsingCommitHooks
    {
        private const int HeadStreamRevision = 5;
        private const int HeadCommitSequence = 1;
        private const int ExpectedNextCommitSequence = HeadCommitSequence + 1;
        private const int BeyondEndOfStreamCommitSequence = ExpectedNextCommitSequence + 1;
        private ICommit _alreadyCommitted = null!;
        private CommitAttempt _beyondEndOfStream = null!;
        private Exception _thrown = null!;

        protected override Task Context()
        {
            _alreadyCommitted = BuildCommitStub(HeadStreamRevision, HeadCommitSequence);
            _beyondEndOfStream = BuildCommitAttemptStub(HeadStreamRevision + 1, BeyondEndOfStreamCommitSequence);

            Hook.PostCommit(_alreadyCommitted);

            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            _thrown = (await Catch.Exception(() => Hook.PreCommit(_beyondEndOfStream)).ConfigureAwait(false))!;
        }

        [Fact]
        public void should_throw_a_PersistenceException()
        {
            Assert.IsType<StorageException>(_thrown);
        }
    }

    public class WhenCommittingWithARevisionBeyondTheKnownEndOfAStream : UsingCommitHooks
    {
        private const int HeadCommitSequence = 1;
        private const int HeadStreamRevision = 1;
        private const int NumberOfEventsBeingCommitted = 1;
        private const int ExpectedNextStreamRevision = HeadStreamRevision + 1 + NumberOfEventsBeingCommitted;
        private const int BeyondEndOfStreamRevision = ExpectedNextStreamRevision + 1;
        private ICommit _alreadyCommitted = null!;
        private CommitAttempt _beyondEndOfStream = null!;
        private Exception _thrown = null!;

        protected override async Task Context()
        {
            _alreadyCommitted = BuildCommitStub(HeadStreamRevision, HeadCommitSequence);
            _beyondEndOfStream = BuildCommitAttemptStub(BeyondEndOfStreamRevision, HeadCommitSequence + 1);

            await Hook.PostCommit(_alreadyCommitted).ConfigureAwait(false);
        }

        protected override async Task Because()
        {
            _thrown = (await Catch.Exception(() => Hook.PreCommit(_beyondEndOfStream)).ConfigureAwait(false))!;
        }

        [Fact]
        public void should_throw_a_StorageException()
        {
            Assert.IsType<StorageException>(_thrown);
        }
    }

    public class
        WhenCommittingWithASequenceLessOrEqualToTheMostRecentSequenceForTheStream :
            UsingCommitHooks
    {
        private const int HeadStreamRevision = 42;
        private const int HeadCommitSequence = 42;
        private const int DupliateCommitSequence = HeadCommitSequence;
        private CommitAttempt _attempt = null!;
        private ICommit _committed = null!;
        private Exception _thrown = null!;

        protected override Task Context()
        {
            _committed = BuildCommitStub(HeadStreamRevision, HeadCommitSequence);
            _attempt = BuildCommitAttemptStub(HeadStreamRevision + 1, DupliateCommitSequence);

            Hook.PostCommit(_committed);

            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            _thrown = (await Catch.Exception(() => Hook.PreCommit(_attempt)).ConfigureAwait(false))!;
        }

        [Fact]
        public void should_throw_a_ConcurrencyException()
        {
            Assert.IsType<ConcurrencyException>(_thrown);
        }


        [Fact]
        public void ConcurrencyException_should_have_good_message()
        {
            Assert.Contains(_attempt.StreamId, _thrown.Message);
            Assert.Contains("CommitSequence [" + _attempt.CommitSequence, _thrown.Message);
        }
    }

    public class
        WhenCommittingWithARevisionLessOrEqualToThanTheMostRecentRevisionReadForTheStream :
            UsingCommitHooks
    {
        private const int HeadStreamRevision = 3;
        private const int HeadCommitSequence = 2;
        private const int DuplicateStreamRevision = HeadStreamRevision;
        private ICommit _committed = null!;
        private CommitAttempt _failedAttempt = null!;
        private Exception _thrown = null!;

        protected override Task Context()
        {
            _committed = BuildCommitStub(HeadStreamRevision, HeadCommitSequence);
            _failedAttempt = BuildCommitAttemptStub(DuplicateStreamRevision, HeadCommitSequence + 1);

            Hook.PostCommit(_committed);

            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            _thrown = (await Catch.Exception(() => Hook.PreCommit(_failedAttempt)).ConfigureAwait(false))!;
        }

        [Fact]
        public void should_throw_a_ConcurrencyException()
        {
            Assert.IsType<ConcurrencyException>(_thrown);
        }

        [Fact]
        public void ConcurrencyException_should_have_good_message()
        {
            Assert.Contains(_failedAttempt.StreamId, _thrown.Message);
            Assert.Contains("StreamRevision [" + _failedAttempt.CommitSequence, _thrown.Message);
        }
    }

    public class
        WhenCommittingWithACommitSequenceLessThanOrEqualToTheMostRecentCommitForTheStream :
            UsingCommitHooks
    {
        private const int DuplicateCommitSequence = 1;
        private CommitAttempt _failedAttempt = null!;
        private ICommit _successfulAttempt = null!;
        private Exception _thrown = null!;

        protected override async Task Context()
        {
            _successfulAttempt = BuildCommitStub(1, DuplicateCommitSequence);
            _failedAttempt = BuildCommitAttemptStub(2, DuplicateCommitSequence);

            await Hook.PostCommit(_successfulAttempt).ConfigureAwait(false);
        }

        protected override async Task Because()
        {
            _thrown = (await Catch.Exception(() => Hook.PreCommit(_failedAttempt)).ConfigureAwait(false))!;
        }

        [Fact]
        public void should_throw_a_ConcurrencyException()
        {
            Assert.IsType<ConcurrencyException>(_thrown);
        }

        [Fact]
        public void ConcurrencyException_should_have_good_message()
        {
            Assert.Contains(_failedAttempt.StreamId, _thrown.Message);
            Assert.Contains("CommitSequence [" + _failedAttempt.CommitSequence, _thrown.Message);
        }
    }

    public class
        WhenCommittingWithAStreamRevisionLessThanOrEqualToTheMostRecentCommitForTheStream :
            UsingCommitHooks
    {
        private const int DuplicateStreamRevision = 2;

        private CommitAttempt _failedAttempt = null!;
        private ICommit _successfulAttempt = null!;
        private Exception _thrown = null!;

        protected override Task Context()
        {
            _successfulAttempt = BuildCommitStub(DuplicateStreamRevision, 1);
            _failedAttempt = BuildCommitAttemptStub(DuplicateStreamRevision, 2);

            Hook.PostCommit(_successfulAttempt);

            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            _thrown = (await Catch.Exception(() => Hook.PreCommit(_failedAttempt)).ConfigureAwait(false))!;
        }

        [Fact]
        public void should_throw_a_ConcurrencyException()
        {
            Assert.IsType<ConcurrencyException>(_thrown);
        }

        [Fact]
        public void Concurrency_exception_should_have_good_message()
        {
            Assert.Contains(_failedAttempt.StreamId, _thrown.Message);
            Assert.Contains(_failedAttempt.StreamRevision.ToString(), _thrown.Message);
        }
    }

    public class WhenTrackingCommits : SpecificationBase
    {
        private const int MaxStreamsToTrack = 2;
        private ICommit[] _trackedCommitAttempts = null!;
        private OptimisticPipelineHook _hook = null!;

        public WhenTrackingCommits()
        {
            OnStart().Wait();
        }

        protected override Task Context()
        {
            _trackedCommitAttempts = new[]
            {
                BuildCommit(Guid.NewGuid(), Guid.NewGuid()),
                BuildCommit(Guid.NewGuid(), Guid.NewGuid()),
                BuildCommit(Guid.NewGuid(), Guid.NewGuid())
            };

            _hook = new OptimisticPipelineHook(MaxStreamsToTrack, NullLogger.Instance);

            return Task.CompletedTask;
        }

        protected override Task Because()
        {
            foreach (var commit in _trackedCommitAttempts)
            {
                _hook.Track(commit);
            }

            return Task.CompletedTask;
        }

        [Fact]
        public void should_only_contain_streams_explicitly_tracked()
        {
            var untracked = BuildCommit(Guid.Empty, _trackedCommitAttempts[0].CommitId);
            Assert.False(_hook.Contains(untracked));
        }

        [Fact]
        public void should_find_tracked_streams()
        {
            var stillTracked = BuildCommit(
                _trackedCommitAttempts.Last().StreamId,
                _trackedCommitAttempts.Last().CommitId);
            Assert.True(_hook.Contains(stillTracked));
        }

        [Fact]
        public void should_only_track_the_specified_number_of_streams()
        {
            var droppedFromTracking = BuildCommit(
                _trackedCommitAttempts.First().StreamId,
                _trackedCommitAttempts.First().CommitId);
            Assert.False(_hook.Contains(droppedFromTracking));
        }

        private ICommit BuildCommit(Guid streamId, Guid commitId) => BuildCommit(streamId.ToString(), commitId);

        private ICommit BuildCommit(string streamId, Guid commitId) => new Commit(Bucket.Default, streamId, 0,
            commitId, 0, SystemTime.UtcNow, 0, null, null);
    }

    public class WhenPurging : SpecificationBase
    {
        private ICommit _trackedCommit = null!;
        private OptimisticPipelineHook _hook = null!;

        public WhenPurging()
        {
            OnStart().Wait();
        }

        protected override Task Context()
        {
            _trackedCommit = BuildCommit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            _hook = new OptimisticPipelineHook(NullLogger.Instance);
            _hook.Track(_trackedCommit);

            return Task.CompletedTask;
        }

        protected override Task Because()
        {
            _hook.OnPurge();

            return Task.CompletedTask;
        }

        [Fact]
        public void should_not_track_commit()
        {
            Assert.False(_hook.Contains(_trackedCommit));
        }

        private ICommit BuildCommit(Guid bucketId, Guid streamId, Guid commitId) =>
            new Commit(
                bucketId.ToString(),
                streamId.ToString(),
                0,
                commitId,
                0,
                SystemTime.UtcNow,
                0,
                null,
                null);
    }

    public class WhenPurgingABucket : SpecificationBase
    {
        private ICommit _trackedCommitBucket1 = null!;
        private ICommit _trackedCommitBucket2 = null!;
        private OptimisticPipelineHook _hook = null!;

        public WhenPurgingABucket()
        {
            OnStart().Wait();
        }

        protected override Task Context()
        {
            _trackedCommitBucket1 = BuildCommit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            _trackedCommitBucket2 = BuildCommit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            _hook = new OptimisticPipelineHook(NullLogger.Instance);
            _hook.Track(_trackedCommitBucket1);
            _hook.Track(_trackedCommitBucket2);

            return Task.CompletedTask;
        }

        protected override Task Because()
        {
            _hook.OnPurge(_trackedCommitBucket1.BucketId);

            return Task.CompletedTask;
        }

        [Fact]
        public void should_not_track_the_commit_in_bucket()
        {
            Assert.False(_hook.Contains(_trackedCommitBucket1));
        }

        [Fact]
        public void should_track_the_commit_in_other_bucket()
        {
            Assert.True(_hook.Contains(_trackedCommitBucket2));
        }

        private ICommit BuildCommit(Guid bucketId, Guid streamId, Guid commitId) =>
            new Commit(
                bucketId.ToString(),
                streamId.ToString(),
                0,
                commitId,
                0,
                SystemTime.UtcNow,
                0,
                null,
                null);
    }

    public class WhenDeletingAStream : SpecificationBase
    {
        private ICommit _trackedCommit = null!;
        private ICommit _trackedCommitDeleted = null!;
        private OptimisticPipelineHook _hook = null!;
        private readonly Guid _bucketId = Guid.NewGuid();
        private readonly Guid _streamIdDeleted = Guid.NewGuid();

        public WhenDeletingAStream()
        {
            OnStart().Wait();
        }

        protected override Task Context()
        {
            _trackedCommit = BuildCommit(_bucketId, Guid.NewGuid(), Guid.NewGuid());
            _trackedCommitDeleted = BuildCommit(_bucketId, _streamIdDeleted, Guid.NewGuid());
            _hook = new OptimisticPipelineHook(NullLogger.Instance);
            _hook.Track(_trackedCommit);
            _hook.Track(_trackedCommitDeleted);

            return Task.CompletedTask;
        }

        protected override Task Because() =>
            _hook.OnDeleteStream(_trackedCommitDeleted.BucketId, _trackedCommitDeleted.StreamId);

        [Fact]
        public void should_not_track_the_commit_in_the_deleted_stream()
        {
            Assert.False(_hook.Contains(_trackedCommitDeleted));
        }

        [Fact]
        public void should_track_the_commit_that_is_not_in_the_deleted_stream()
        {
            Assert.True(_hook.Contains(_trackedCommit));
        }

        private ICommit BuildCommit(Guid bucketId, Guid streamId, Guid commitId) =>
            new Commit(
                bucketId.ToString(),
                streamId.ToString(),
                0,
                commitId,
                0,
                SystemTime.UtcNow,
                0,
                null,
                null);
    }

    public abstract class UsingCommitHooks : SpecificationBase
    {
        protected readonly OptimisticPipelineHook Hook = new(NullLogger.Instance);
        private readonly string _streamId = Guid.NewGuid().ToString();

        protected UsingCommitHooks()
        {
            OnStart().Wait();
        }

        protected ICommit BuildCommitStub(int streamRevision, int commitSequence)
        {
            var events = new[] { new EventMessage(new object()) }.ToList();
            return new Commit(
                Bucket.Default,
                _streamId,
                streamRevision,
                Guid.NewGuid(),
                commitSequence,
                SystemTime.UtcNow,
                0,
                null,
                events);
        }

        protected CommitAttempt BuildCommitAttemptStub(int streamRevision, int commitSequence)
        {
            var events = new[] { new EventMessage(new object()) }.ToList();
            return new CommitAttempt(
                Bucket.Default,
                _streamId,
                streamRevision,
                Guid.NewGuid(),
                commitSequence,
                SystemTime.UtcNow,
                null,
                events);
        }
    }
}
