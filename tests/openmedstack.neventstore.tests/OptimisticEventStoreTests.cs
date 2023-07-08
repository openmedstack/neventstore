using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using FakeItEasy;
    using Microsoft.Extensions.Logging.Abstractions;
    using NEventStore;
    using NEventStore.Persistence;
    using NEventStore.Persistence.AcceptanceTests;
    using NEventStore.Persistence.AcceptanceTests.BDD;
    using Xunit;

    public class WhenCreatingANewStream : UsingPersistence
    {
        private IEventStream _stream = null!;

        protected override async Task Because()
        {
            _stream = await Store.CreateStream(StreamId).ConfigureAwait(false);
        }

        [Fact]
        public void should_return_a_new_stream()
        {
            Assert.NotNull(_stream);
        }

        [Fact]
        public void should_return_a_stream_with_the_correct_stream_identifier()
        {
            Assert.Equal(StreamId, _stream.StreamId);
        }

        [Fact]
        public void should_return_a_stream_with_a_zero_stream_revision()
        {
            Assert.Equal(0, _stream.StreamRevision);
        }

        [Fact]
        public void should_return_a_stream_with_a_zero_commit_sequence()
        {
            Assert.Equal(0, _stream.CommitSequence);
        }

        [Fact]
        public void should_return_a_stream_with_no_uncommitted_events()
        {
            Assert.Empty(_stream.UncommittedEvents);
        }

        [Fact]
        public void should_return_a_stream_with_no_committed_events()
        {
            Assert.Empty(_stream.CommittedEvents);
        }

        [Fact]
        public void should_return_a_stream_with_empty_headers()
        {
            Assert.Empty(_stream.UncommittedHeaders);
        }
    }

    public class WhenOpeningAnEmptyStreamStartingAtRevisionZero : UsingPersistence
    {
        private IEventStream _stream = null!;

        protected override Task Context()
        {
            A.CallTo(() => Persistence.GetFrom(Bucket.Default, StreamId, 0, 0, default))
                .Returns(Array.Empty<ICommit>().ToAsyncEnumerable());
            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            _stream = await Store.OpenStream(StreamId, 0, 0).ConfigureAwait(false);
        }

        [Fact]
        public void should_return_a_new_stream()
        {
            Assert.NotNull(_stream);
        }

        [Fact]
        public void should_return_a_stream_with_the_correct_stream_identifier()
        {
            Assert.Equal(StreamId, _stream.StreamId);
        }

        [Fact]
        public void should_return_a_stream_with_a_zero_stream_revision()
        {
            Assert.Equal(0, _stream.StreamRevision);
        }

        [Fact]
        public void should_return_a_stream_with_a_zero_commit_sequence()
        {
            Assert.Equal(0, _stream.CommitSequence);
        }

        [Fact]
        public void should_return_a_stream_with_no_uncommitted_events()
        {
            Assert.Empty(_stream.UncommittedEvents);
        }

        [Fact]
        public void should_return_a_stream_with_no_committed_events()
        {
            Assert.Empty(_stream.CommittedEvents);
        }

        [Fact]
        public void should_return_a_stream_with_empty_headers()
        {
            Assert.Empty(_stream.UncommittedHeaders);
        }
    }

    public class WhenOpeningAnEmptyStreamStartingAboveRevisionZero : UsingPersistence
    {
        private const int MinRevision = 1;
        private Exception _thrown = null!;

        protected override Task Context()
        {
            A.CallTo(() => Persistence.GetFrom(Bucket.Default, StreamId, MinRevision, int.MaxValue, default))
                .Returns(Enumerable.Empty<ICommit>().ToAsyncEnumerable());

            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            _thrown = (await Catch.Exception(() => Store.OpenStream(StreamId, MinRevision)).ConfigureAwait(false))!;
        }

        [Fact]
        public void should_throw_a_StreamNotFoundException()
        {
            Assert.IsType<StreamNotFoundException>(_thrown);
        }
    }

    public class WhenOpeningAPopulatedStream : UsingPersistence
    {
        private const int MinRevision = 17;
        private const int MaxRevision = 42;
        private ICommit _committed = null!;
        private IEventStream _stream = null!;

        protected override Task Context()
        {
            _committed = BuildCommitStub(MinRevision, 1);

            A.CallTo(() => Persistence.GetFrom(Bucket.Default, StreamId, MinRevision, MaxRevision, default))
                .Returns(new[] { _committed }.ToAsyncEnumerable());

            var hook = A.Fake<IPipelineHook>();
            A.CallTo(() => hook.Select(_committed)).Returns(_committed);
            PipelineHooks.Add(hook);

            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            _stream = await Store.OpenStream(StreamId, MinRevision, MaxRevision).ConfigureAwait(false);
        }

        [Fact]
        public void should_invoke_the_underlying_infrastructure_with_the_values_provided()
        {
            A.CallTo(() => Persistence.GetFrom(Bucket.Default, StreamId, MinRevision, MaxRevision, default))
                .MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public void should_provide_the_commits_to_the_selection_hooks()
        {
            PipelineHooks.ForEach(x => A.CallTo(() => x.Select(_committed)).MustHaveHappened(1, Times.Exactly));
        }

        [Fact]
        public void should_return_an_event_stream_containing_the_correct_stream_identifer()
        {
            Assert.Equal(StreamId, _stream.StreamId);
        }
    }

    public class WhenOpeningAPopulatedStreamFromASnapshot : UsingPersistence
    {
        private const int MaxRevision = int.MaxValue;
        private ICommit[] _committed = null!;
        private Snapshot _snapshot = null!;

        protected override Task Context()
        {
            _snapshot = new Snapshot(StreamId, 42, "snapshot");
            _committed = new[] { BuildCommitStub(42, 0) };

            A.CallTo(() => Persistence.GetFrom(Bucket.Default, StreamId, 42, MaxRevision, default))
                .Returns(_committed.ToAsyncEnumerable());

            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            await Store.OpenStream(_snapshot, MaxRevision, default).ConfigureAwait(false);
        }

        [Fact]
        public void should_query_the_underlying_storage_using_the_revision_of_the_snapshot()
        {
            A.CallTo(() => Persistence.GetFrom(Bucket.Default, StreamId, 42, MaxRevision, default))
                .MustHaveHappened(1, Times.Exactly);
        }
    }

    public class WhenOpeningAStreamFromASnapshotThatIsAtTheRevisionOfTheStreamHead : UsingPersistence
    {
        private const int HeadStreamRevision = 42;
        private const int HeadCommitSequence = 15;
        private EnumerableCounter<ICommit> _committed = null!;
        private Snapshot _snapshot = null!;
        private IEventStream _stream = null!;

        protected override Task Context()
        {
            _snapshot = new Snapshot(StreamId, HeadStreamRevision, "snapshot");
            _committed = new EnumerableCounter<ICommit>(
                new[] { BuildCommitStub(HeadStreamRevision, HeadCommitSequence) });

            A.CallTo(() => Persistence.GetFrom(Bucket.Default, StreamId, HeadStreamRevision, int.MaxValue, default))
                .Returns(_committed.ToAsyncEnumerable());

            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            _stream = await Store.OpenStream(_snapshot, int.MaxValue, default).ConfigureAwait(false);
        }

        [Fact]
        public void should_return_a_stream_with_the_correct_stream_identifier()
        {
            Assert.Equal(StreamId, _stream.StreamId);
        }

        [Fact]
        public void should_return_a_stream_with_revision_of_the_stream_head()
        {
            Assert.Equal(HeadStreamRevision, _stream.StreamRevision);
        }

        [Fact]
        public void should_return_a_stream_with_a_commit_sequence_of_the_stream_head()
        {
            Assert.Equal(HeadCommitSequence, _stream.CommitSequence);
        }

        [Fact]
        public void should_return_a_stream_with_no_committed_events()
        {
            Assert.Empty(_stream.CommittedEvents);
        }

        [Fact]
        public void should_return_a_stream_with_no_uncommitted_events()
        {
            Assert.Empty(_stream.UncommittedEvents);
        }

        [Fact]
        public void should_only_enumerate_the_set_of_commits_once()
        {
            Assert.Equal(1, _committed.GetEnumeratorCallCount);
        }
    }

    public class WhenReadingFromRevisionZero : UsingPersistence
    {
        protected override Task Context()
        {
            A.CallTo(() => Persistence.GetFrom(Bucket.Default, StreamId, 0, int.MaxValue, default))
                .Returns(Enumerable.Empty<ICommit>().ToAsyncEnumerable());

            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
            // This forces the enumeration of the commits.
            _ = await Store.GetFrom(StreamId, 0, int.MaxValue, default).ToList().ConfigureAwait(false);
        }

        [Fact]
        public void should_pass_a_revision_range_to_the_persistence_infrastructure()
        {
            A.CallTo(() => Persistence.GetFrom(Bucket.Default, StreamId, 0, int.MaxValue, default))
                .MustHaveHappened(1, Times.Exactly);
        }
    }

    public class WhenReadingUpToRevisionRevisionZero : UsingPersistence
    {
        private ICommit _committed = null!;

        protected override Task Context()
        {
            _committed = BuildCommitStub(1, 1);

            A.CallTo(() => Persistence.GetFrom(Bucket.Default, StreamId, 0, int.MaxValue, default))
                .Returns(new[] { _committed }.ToAsyncEnumerable());

            return Task.CompletedTask;
        }

        protected override Task Because() => Store.OpenStream(StreamId, 0, 0);

        [Fact]
        public void should_pass_the_maximum_possible_revision_to_the_persistence_infrastructure()
        {
            A.CallTo(() => Persistence.GetFrom(Bucket.Default, StreamId, 0, int.MaxValue, default))
                .MustHaveHappened(1, Times.Exactly);
        }
    }

    //public class WhenReadingFromANullSnapshot : UsingPersistence
    //{
    //    private Exception _thrown;

    //    protected override async Task Because()
    //    {
    //        _thrown = (await Catch.Exception(() => Store.OpenStream(null, int.MaxValue)).ConfigureAwait(false))!;
    //    }

    //    [Fact]
    //    public void should_throw_an_ArgumentNullException()
    //    {
    //        _thrown.Should().BeOfType<ArgumentNullException>();
    //    }
    //}

    public class WhenReadingFromASnapshotUpToRevisionRevisionZero : UsingPersistence
    {
        private ICommit _committed = null!;
        private Snapshot _snapshot = null!;

        protected override Task Context()
        {
            _snapshot = new Snapshot(StreamId, 1, "snapshot");
            _committed = BuildCommitStub(1, 1);

            A.CallTo(
                    () => Persistence.GetFrom(
                        Bucket.Default,
                        StreamId,
                        _snapshot.StreamRevision,
                        int.MaxValue,
                        default))
                .Returns(new[] { _committed }.ToAsyncEnumerable());

            return Task.CompletedTask;
        }

        protected override Task Because() => Store.OpenStream(_snapshot, 0, default);

        [Fact]
        public void should_pass_the_maximum_possible_revision_to_the_persistence_infrastructure()
        {
            A.CallTo(
                    () => Persistence.GetFrom(
                        Bucket.Default,
                        StreamId,
                        _snapshot.StreamRevision,
                        int.MaxValue,
                        default))
                .MustHaveHappened(1, Times.Exactly);
        }
    }

    //public class WhenCommittingANullAttemptBackToTheStream : UsingPersistence
    //{
    //    private Exception _thrown;

    //    protected override async Task Because()
    //    {
    //        _thrown = await Catch.Exception(() => ((ICommitEvents)Store).Commit(null)).ConfigureAwait(false);
    //    }

    //    [Fact]
    //    public void should_throw_an_ArgumentNullException()
    //    {
    //        _thrown.Should().BeOfType<ArgumentNullException>();
    //    }
    //}

    public class WhenCommittingWithAValidAndPopulatedAttemptToAStream : UsingPersistence
    {
        private CommitAttempt _populatedAttempt = null!;
        private ICommit _populatedCommit = null!;

        protected override Task Context()
        {
            _populatedAttempt = BuildCommitAttemptStub(1, 1);

            A.CallTo(() => Persistence.Commit(_populatedAttempt))
                .ReturnsLazily(
                    (CommitAttempt attempt) =>
                    {
                        _populatedCommit = new Commit(
                            attempt.BucketId,
                            attempt.StreamId,
                            attempt.StreamRevision,
                            attempt.CommitId,
                            attempt.CommitSequence,
                            attempt.CommitStamp,
                            0,
                            attempt.Headers,
                            attempt.Events);
                        return _populatedCommit;
                    });

            var hook = A.Fake<IPipelineHook>();
            A.CallTo(() => hook.PreCommit(_populatedAttempt)).Returns(true);

            PipelineHooks.Add(hook);

            return Task.CompletedTask;
        }

        protected override Task Because() => ((ICommitEvents)Store).Commit(_populatedAttempt);

        [Fact]
        public void should_provide_the_commit_to_the_precommit_hooks()
        {
            PipelineHooks.ForEach(
                x => A.CallTo(() => x.PreCommit(_populatedAttempt)).MustHaveHappened(1, Times.Exactly));
        }

        [Fact]
        public void should_provide_the_commit_attempt_to_the_configured_persistence_mechanism()
        {
            A.CallTo(() => Persistence.Commit(_populatedAttempt)).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public void should_provide_the_commit_to_the_postcommit_hooks()
        {
            PipelineHooks.ForEach(
                x => A.CallTo(() => x.PostCommit(_populatedCommit)).MustHaveHappened(1, Times.Exactly));
        }
    }

    public class WhenAPrecommitHookRejectsACommit : UsingPersistence
    {
        private CommitAttempt _attempt = null!;
        private ICommit _commit = null!;

        protected override Task Context()
        {
            _attempt = BuildCommitAttemptStub(1, 1);
            _commit = BuildCommitStub(1, 1);

            var hook = A.Fake<IPipelineHook>();
            A.CallTo(() => hook.PreCommit(_attempt)).Returns(false);

            PipelineHooks.Add(hook);

            return Task.CompletedTask;
        }

        protected override Task Because() => ((ICommitEvents)Store).Commit(_attempt);

        [Fact]
        public void should_not_call_the_underlying_infrastructure()
        {
            A.CallTo(() => Persistence.Commit(_attempt)).MustNotHaveHappened();
        }

        [Fact]
        public void should_not_provide_the_commit_to_the_postcommit_hooks()
        {
            PipelineHooks.ForEach(x => A.CallTo(() => x.PostCommit(_commit)).MustNotHaveHappened());
        }
    }

    public class WhenDisposingTheEventStore : UsingPersistence
    {
        protected override Task Because()
        {
            Store.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public void should_dispose_the_underlying_persistence()
        {
            A.CallTo(() => Persistence.Dispose()).MustHaveHappened(1, Times.Exactly);
        }
    }

    public abstract class UsingPersistence : SpecificationBase
    {
        private IPersistStreams? _persistence;
        private List<IPipelineHook>? _pipelineHooks;
        private OptimisticEventStore? _store;
        protected string StreamId = Guid.NewGuid().ToString();

        public UsingPersistence()
        {
            OnStart().Wait();
        }

        protected IPersistStreams Persistence => _persistence ??= A.Fake<IPersistStreams>();

        protected List<IPipelineHook> PipelineHooks => _pipelineHooks ??= new List<IPipelineHook>();

        protected OptimisticEventStore Store
        {
            get
            {
                return _store ??=
                    new OptimisticEventStore(Persistence, PipelineHooks.Select(x => x), NullLogger.Instance);
            }
        }

        protected override void Cleanup()
        {
            StreamId = Guid.NewGuid().ToString();
        }

        protected ICommit BuildCommitStub(int streamRevision, int commitSequence)
        {
            var events = new[] { new EventMessage(new object()) }.ToList();
            return new Commit(
                Bucket.Default,
                StreamId,
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
                StreamId,
                streamRevision,
                Guid.NewGuid(),
                commitSequence,
                SystemTime.UtcNow,
                null,
                events);
        }
    }
}
