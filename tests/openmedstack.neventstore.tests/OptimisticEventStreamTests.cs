namespace OpenMedStack.NEventStore.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using FakeItEasy;
    using FluentAssertions;
    using Microsoft.Extensions.Logging.Abstractions;
    using NEventStore;
    using NEventStore.Persistence;
    using NEventStore.Persistence.AcceptanceTests;
    using NEventStore.Persistence.AcceptanceTests.BDD;
    using Xunit;

    public class WhenBuildingAStream : OnTheEventStream
    {
        private const int MinRevision = 2;
        private const int MaxRevision = 7;
        private readonly int _eachCommitHas = 2.Events();
        private ICommit[] _committed = null!;

        public WhenBuildingAStream(FakeTimeFixture fixture)
            : base(fixture)
        {
        }

        protected override Task Context()
        {
            _committed = new[]
            {
                BuildCommitStub(2, 1, _eachCommitHas), // 1-2
                BuildCommitStub(4, 2, _eachCommitHas), // 3-4
                BuildCommitStub(6, 3, _eachCommitHas), // 5-6
                BuildCommitStub(8, 3, _eachCommitHas) // 7-8
            };

            _committed[0].Headers["Common"] = string.Empty;
            _committed[1].Headers["Common"] = string.Empty;
            _committed[2].Headers["Common"] = string.Empty;
            _committed[3].Headers["Common"] = string.Empty;
            _committed[0].Headers["Unique"] = string.Empty;

            A.CallTo(() => Persistence.GetFrom(BucketId, StreamId, MinRevision, MaxRevision, default))
                .Returns(_committed.ToAsyncEnumerable());

            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            Stream = await OptimisticEventStream
                .Create(BucketId, StreamId, Persistence, MinRevision, MaxRevision, NullLogger.Instance, default)
                .ConfigureAwait(false);
        }

        [Fact]
        public void should_have_the_correct_stream_identifier()
        {
            Stream.StreamId.Should().Be(StreamId);
        }

        [Fact]
        public void should_have_the_correct_head_stream_revision()
        {
            Stream.StreamRevision.Should().Be(MaxRevision);
        }

        [Fact]
        public void should_have_the_correct_head_commit_sequence()
        {
            Stream.CommitSequence.Should().Be(_committed.Last().CommitSequence);
        }

        [Fact]
        public void should_not_include_events_below_the_minimum_revision_indicated()
        {
            Stream.CommittedEvents.First().Should().Be(_committed.First().Events.Last());
        }

        [Fact]
        public void should_not_include_events_above_the_maximum_revision_indicated()
        {
            Stream.CommittedEvents.Last().Should().Be(_committed.Last().Events.First());
        }

        [Fact]
        public void should_have_all_of_the_committed_events_up_to_the_stream_revision_specified()
        {
            Stream.CommittedEvents.Count.Should().Be(MaxRevision - MinRevision + 1);
        }

        [Fact]
        public void should_contain_the_headers_from_the_underlying_commits()
        {
            Stream.CommittedHeaders.Count.Should().Be(2);
        }
    }

    public class WhenTheHeadEventRevisionIsLessThanTheMaxDesiredRevision : OnTheEventStream
    {
        private readonly int _eventsPerCommit = 2.Events();
        private ICommit[] _committed = null!;

        public WhenTheHeadEventRevisionIsLessThanTheMaxDesiredRevision(FakeTimeFixture fixture)
            : base(fixture)
        {
        }

        protected override Task Context()
        {
            _committed = new[]
            {
                BuildCommitStub(2, 1, _eventsPerCommit), // 1-2
                BuildCommitStub(4, 2, _eventsPerCommit), // 3-4
                BuildCommitStub(6, 3, _eventsPerCommit), // 5-6
                BuildCommitStub(8, 3, _eventsPerCommit) // 7-8
            };

            A.CallTo(() => Persistence.GetFrom(BucketId, StreamId, 0, int.MaxValue, default))
                .Returns(_committed.ToAsyncEnumerable());

            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            Stream = await OptimisticEventStream.Create(BucketId, StreamId, Persistence, 0, int.MaxValue, NullLogger.Instance, default)
                .ConfigureAwait(false);
        }

        [Fact]
        public void should_set_the_stream_revision_to_the_revision_of_the_most_recent_event()
        {
            Stream.StreamRevision.Should().Be(_committed.Last().StreamRevision);
        }
    }

    //public class WhenAddingANullEventMessage : OnTheEventStream
    //{
    //    private Exception _thrown;

    //    public WhenAddingANullEventMessage(FakeTimeFixture fixture)
    //        : base(fixture)
    //    {
    //    }

    //    protected override Task Because()
    //    {
    //        _thrown = Catch.Exception(() => Stream.Add(null))!;

    //        return Task.CompletedTask;
    //    }

    //    [Fact]
    //    public void should_throw()
    //    {
    //        _thrown.Should().BeOfType<ArgumentNullException>();
    //    }
    //}

    //public class WhenAddingAnUnpopulatedEventMessage : OnTheEventStream
    //{
    //    private Exception _thrown;

    //    public WhenAddingAnUnpopulatedEventMessage(FakeTimeFixture fixture)
    //        : base(fixture)
    //    {
    //    }

    //    protected override Task Because()
    //    {
    //        _thrown = (Catch.Exception(() => Stream.Add(new EventMessage(null))))!;

    //        return Task.CompletedTask;
    //    }

    //    [Fact]
    //    public void should_throw()
    //    {
    //        _thrown.Should().BeOfType<Exception>();
    //    }
    //}

    public class WhenAddingAFullyPopulatedEventMessage : OnTheEventStream
    {
        public WhenAddingAFullyPopulatedEventMessage(FakeTimeFixture fixture)
            : base(fixture)
        {
        }

        protected override Task Because()
        {
            Stream.Add(new EventMessage("populated"));

            return Task.CompletedTask;
        }

        [Fact]
        public void should_add_the_event_to_the_set_of_uncommitted_events()
        {
            Stream.UncommittedEvents.Count.Should().Be(1);
        }
    }

    public class WhenAddingMultiplePopulatedEventMessages : OnTheEventStream
    {
        public WhenAddingMultiplePopulatedEventMessages(FakeTimeFixture fixture)
            : base(fixture)
        {
        }

        protected override Task Because()
        {
            Stream.Add(new EventMessage("populated"));
            Stream.Add(new EventMessage("also populated"));

            return Task.CompletedTask;
        }

        [Fact]
        public void should_add_all_of_the_events_provided_to_the_set_of_uncommitted_events()
        {
            Stream.UncommittedEvents.Count.Should().Be(2);
        }
    }

    public class WhenAddingASimpleObjectAsAnEventMessage : OnTheEventStream
    {
        private const string MyEvent = "some event data";

        public WhenAddingASimpleObjectAsAnEventMessage(FakeTimeFixture fixture)
            : base(fixture)
        {
        }

        protected override Task Because()
        {
            Stream.Add(new EventMessage(MyEvent));

            return Task.CompletedTask;
        }

        [Fact]
        public void should_add_the_uncommited_event_to_the_set_of_uncommitted_events()
        {
            Stream.UncommittedEvents.Count.Should().Be(1);
        }

        [Fact]
        public void should_wrap_the_uncommited_event_in_an_EventMessage_object()
        {
            Stream.UncommittedEvents.First().Body.Should().Be(MyEvent);
        }
    }

    public class WhenClearingAnyUncommittedChanges : OnTheEventStream
    {
        public WhenClearingAnyUncommittedChanges(FakeTimeFixture fixture)
            : base(fixture)
        {
        }

        protected override Task Context()
        {
            Stream.Add(new EventMessage(string.Empty));

            return Task.CompletedTask;
        }

        protected override Task Because()
        {
            Stream.ClearChanges();

            return Task.CompletedTask;
        }

        [Fact]
        public void should_clear_all_uncommitted_events()
        {
            Stream.UncommittedEvents.Count.Should().Be(0);
        }
    }

    public class WhenCommittingAnEmptyChangeset : OnTheEventStream
    {
        public WhenCommittingAnEmptyChangeset(FakeTimeFixture fixture)
            : base(fixture)
        {
        }

        protected override Task Because() => Stream.CommitChanges(Guid.NewGuid(), default);

        [Fact]
        public void should_not_call_the_underlying_infrastructure()
        {
            A.CallTo(() => Persistence.Commit(A<CommitAttempt>._)).MustNotHaveHappened();
        }

        [Fact]
        public void should_not_increment_the_current_stream_revision()
        {
            Stream.StreamRevision.Should().Be(0);
        }

        [Fact]
        public void should_not_increment_the_current_commit_sequence()
        {
            Stream.CommitSequence.Should().Be(0);
        }
    }

    public class WhenCommittingAnyUncommittedChanges : OnTheEventStream
    {
        private readonly Guid _commitId = Guid.NewGuid();
        private readonly Dictionary<string, object> _headers = new() { { "key", "value" } };
        private readonly EventMessage _uncommitted = new(string.Empty);
        private CommitAttempt _constructed = null!;

        public WhenCommittingAnyUncommittedChanges(FakeTimeFixture fixture)
            : base(fixture)
        {
        }

        protected override Task Context()
        {
            A.CallTo(() => Persistence.Commit(A<CommitAttempt>._))
                .Invokes((CommitAttempt _) => _constructed = _)
                .ReturnsLazily(
                    (CommitAttempt attempt) => new Commit(
                        attempt.BucketId,
                        attempt.StreamId,
                        attempt.StreamRevision,
                        attempt.CommitId,
                        attempt.CommitSequence,
                        attempt.CommitStamp,
                        0,
                        attempt.Headers,
                        attempt.Events));
            Stream.Add(_uncommitted);
            foreach (var item in _headers)
            {
                Stream.UncommittedHeaders[item.Key] = item.Value;
            }

            return Task.CompletedTask;
        }

        protected override Task Because() => Stream.CommitChanges(_commitId, default);

        [Fact]
        public void should_provide_a_commit_to_the_underlying_infrastructure()
        {
            A.CallTo(() => Persistence.Commit(A<CommitAttempt>._)).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public void should_build_the_commit_with_the_correct_bucket_identifier()
        {
            _constructed.BucketId.Should().Be(BucketId);
        }

        [Fact]
        public void should_build_the_commit_with_the_correct_stream_identifier()
        {
            _constructed.StreamId.Should().Be(StreamId);
        }

        [Fact]
        public void should_build_the_commit_with_the_correct_stream_revision()
        {
            _constructed.StreamRevision.Should().Be(DefaultStreamRevision);
        }

        [Fact]
        public void should_build_the_commit_with_the_correct_commit_identifier()
        {
            _constructed.CommitId.Should().Be(_commitId);
        }

        [Fact]
        public void should_build_the_commit_with_an_incremented_commit_sequence()
        {
            _constructed.CommitSequence.Should().Be(DefaultCommitSequence);
        }

        [Fact]
        public void should_build_the_commit_with_the_correct_commit_stamp()
        {
            SystemTime.UtcNow.Should().BeCloseTo(_constructed.CommitStamp, TimeSpan.FromMilliseconds(10));
        }

        [Fact]
        public void should_build_the_commit_with_the_headers_provided()
        {
            _constructed.Headers[_headers.First().Key].Should().Be(_headers.First().Value);
        }

        [Fact]
        public void should_build_the_commit_containing_all_uncommitted_events()
        {
            _constructed.Events.Count.Should().Be(_headers.Count);
        }

        [Fact]
        public void should_build_the_commit_using_the_event_messages_provided()
        {
            _constructed.Events.First().Should().Be(_uncommitted);
        }

        [Fact]
        public void should_contain_a_copy_of_the_headers_provided()
        {
            _constructed.Headers.Should().NotBeEmpty();
        }

        [Fact]
        public void should_update_the_stream_revision()
        {
            Stream.StreamRevision.Should().Be(_constructed.StreamRevision);
        }

        [Fact]
        public void should_update_the_commit_sequence()
        {
            Stream.CommitSequence.Should().Be(_constructed.CommitSequence);
        }

        [Fact]
        public void should_add_the_uncommitted_events_the_committed_events()
        {
            Stream.CommittedEvents.Last().Should().Be(_uncommitted);
        }

        [Fact]
        public void should_clear_the_uncommitted_events_on_the_stream()
        {
            Stream.UncommittedEvents.Should().BeEmpty();
        }

        [Fact]
        public void should_clear_the_uncommitted_headers_on_the_stream()
        {
            Stream.UncommittedHeaders.Should().BeEmpty();
        }

        [Fact]
        public void should_copy_the_uncommitted_headers_to_the_committed_stream_headers()
        {
            Stream.CommittedHeaders.Count.Should().Be(_headers.Count);
        }
    }

    /// <summary>
    ///     This behavior is primarily to support a NoSQL storage solution where CommitId is not being used as the "primary key"
    ///     in a NoSQL environment, we'll most likely use StreamId + CommitSequence, which also enables optimistic concurrency.
    /// </summary>
    public class WhenCommittingWithAnIdentifierThatWasPreviouslyRead : OnTheEventStream
    {
        private ICommit[] _committed = null!;
        private Guid _dupliateCommitId;
        private Exception _thrown = null!;

        public WhenCommittingWithAnIdentifierThatWasPreviouslyRead(FakeTimeFixture fixture)
            : base(fixture)
        {
        }

        protected override async Task Context()
        {
            _committed = new[] { BuildCommitStub(1, 1, 1) };
            _dupliateCommitId = _committed[0].CommitId;

            A.CallTo(() => Persistence.GetFrom(BucketId, StreamId, 0, int.MaxValue, default))
                .Returns(_committed.ToAsyncEnumerable());

            Stream = await OptimisticEventStream.Create(BucketId, StreamId, Persistence, 0, int.MaxValue, NullLogger.Instance)
                .ConfigureAwait(false);
        }

        protected override async Task Because()
        {
            _thrown = (await Catch.Exception(() => Stream.CommitChanges(_dupliateCommitId, default))
                .ConfigureAwait(false))!;
        }

        [Fact]
        public void should_throw_a_DuplicateCommitException()
        {
            _thrown.Should().BeOfType<DuplicateCommitException>();
        }
    }

    public class WhenCommittingAfterAnotherThreadOrProcessHasMovedTheStreamHead : OnTheEventStream
    {
        private const int StreamRevision = 1;
        private readonly EventMessage _uncommitted = new(string.Empty);
        private ICommit[] _committed = null!;
        private ICommit[] _discoveredOnCommit = null!;
        private Exception _thrown = null!;

        public WhenCommittingAfterAnotherThreadOrProcessHasMovedTheStreamHead(FakeTimeFixture fixture)
            : base(fixture)
        {
        }

        protected override async Task Context()
        {
            _committed = new[] { BuildCommitStub(1, 1, 1) };
            _discoveredOnCommit = new[] { BuildCommitStub(3, 2, 2) };

            A.CallTo(() => Persistence.Commit(A<CommitAttempt>._)).Throws(new ConcurrencyException());
            A.CallTo(() => Persistence.GetFrom(BucketId, StreamId, StreamRevision, int.MaxValue, default))
                .Returns(_committed.ToAsyncEnumerable());
            A.CallTo(() => Persistence.GetFrom(BucketId, StreamId, StreamRevision + 1, int.MaxValue, default))
                .Returns(_discoveredOnCommit.ToAsyncEnumerable());

            Stream = await OptimisticEventStream
                .Create(BucketId, StreamId, Persistence, StreamRevision, int.MaxValue, NullLogger.Instance)
                .ConfigureAwait(false);
            Stream.Add(_uncommitted);
        }

        protected override async Task Because()
        {
            _thrown = (await Catch.Exception(() => Stream.CommitChanges(Guid.NewGuid(), default)).ConfigureAwait(false))!;
        }

        [Fact]
        public void should_throw_a_ConcurrencyException()
        {
            _thrown.Should().BeOfType<ConcurrencyException>();
        }

        [Fact]
        public void should_query_the_underlying_storage_to_discover_the_new_commits()
        {
            A.CallTo(() => Persistence.GetFrom(BucketId, StreamId, StreamRevision + 1, int.MaxValue, default))
                .MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public void should_update_the_stream_revision_accordingly()
        {
            Stream.StreamRevision.Should().Be(_discoveredOnCommit[0].StreamRevision);
        }

        [Fact]
        public void should_update_the_commit_sequence_accordingly()
        {
            Stream.CommitSequence.Should().Be(_discoveredOnCommit[0].CommitSequence);
        }

        [Fact]
        public void should_add_the_newly_discovered_committed_events_to_the_set_of_committed_events_accordingly()
        {
            Stream.CommittedEvents.Count.Should().Be(_discoveredOnCommit[0].Events.Count + 1);
        }
    }

    public class WhenAttemptingToInvokeBehaviorOnADisposedStream : OnTheEventStream
    {
        private Exception _thrown = null!;

        public WhenAttemptingToInvokeBehaviorOnADisposedStream(FakeTimeFixture fixture)
            : base(fixture)
        {
        }

        protected override Task Context()
        {
            Stream.Dispose();

            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            _thrown = (await Catch.Exception(() => Stream.CommitChanges(Guid.NewGuid(), default)).ConfigureAwait(false))!;
        }

        [Fact]
        public void should_throw_a_ObjectDisposedException()
        {
            _thrown.Should().BeOfType<ObjectDisposedException>();
        }
    }

    public class WhenAttemptingToModifyTheEventCollections : OnTheEventStream
    {
        public WhenAttemptingToModifyTheEventCollections(FakeTimeFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void should_throw_an_exception_when_adding_to_the_committed_collection()
        {
            Catch.Exception(() => Stream.CommittedEvents.Add(new EventMessage(new object()))).Should().BeOfType<NotSupportedException>();
        }

        [Fact]
        public void should_throw_an_exception_when_adding_to_the_uncommitted_collection()
        {
            Catch.Exception(() => Stream.UncommittedEvents.Add(new EventMessage(new object()))).Should().BeOfType<NotSupportedException>();
        }

        [Fact]
        public void should_throw_an_exception_when_clearing_the_committed_collection()
        {
            Catch.Exception(() => Stream.CommittedEvents.Clear()).Should().BeOfType<NotSupportedException>();
        }

        [Fact]
        public void should_throw_an_exception_when_clearing_the_uncommitted_collection()
        {
            Catch.Exception(() => Stream.UncommittedEvents.Clear()).Should().BeOfType<NotSupportedException>();
        }

        [Fact]
        public void should_throw_an_exception_when_removing_from_the_committed_collection()
        {
            Catch.Exception(() => Stream.CommittedEvents.Remove(new EventMessage(new object()))).Should().BeOfType<NotSupportedException>();
        }

        [Fact]
        public void should_throw_an_exception_when_removing_from_the_uncommitted_collection()
        {
            Catch.Exception(() => Stream.UncommittedEvents.Remove(new EventMessage(new object()))).Should().BeOfType<NotSupportedException>();
        }
    }

    public abstract class OnTheEventStream : SpecificationBase, IClassFixture<FakeTimeFixture>
    {
        protected const int DefaultStreamRevision = 1;
        protected const int DefaultCommitSequence = 1;
        private ICommitEvents? _persistence;
        private OptimisticEventStream? _stream;
        protected const string BucketId = "bucket";
        protected readonly string StreamId = Guid.NewGuid().ToString();

        public OnTheEventStream(FakeTimeFixture fixture)
        {
            SetFixture(fixture);
            OnStart().Wait();
        }

        protected ICommitEvents Persistence => _persistence ??= A.Fake<ICommitEvents>();

        protected OptimisticEventStream Stream
        {
            get => _stream ??= OptimisticEventStream.Create(BucketId, StreamId, Persistence, NullLogger.Instance).Result;
            set => _stream = value;
        }

        public void SetFixture(FakeTimeFixture data)
        {
        }

        protected ICommit BuildCommitStub(int revision, int sequence, int eventCount)
        {
            var events = new List<EventMessage>(eventCount);
            for (var i = 0; i < eventCount; i++)
            {
                events.Add(new EventMessage(string.Empty));
            }

            return new Commit(
                Bucket.Default,
                StreamId,
                revision,
                Guid.NewGuid(),
                sequence,
                SystemTime.UtcNow,
                0,
                null,
                events);
        }
    }

    public class FakeTimeFixture : IDisposable
    {
        public FakeTimeFixture()
        {
            SystemTime.Resolver = () => new DateTime(2012, 1, 1, 13, 0, 0);
        }

        public void Dispose()
        {
            SystemTime.Resolver = () => DateTimeOffset.MinValue;
            GC.SuppressFinalize(this);
        }
    }
}
