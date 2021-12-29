namespace OpenMedStack.NEventStore.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FakeItEasy;
    using Microsoft.Extensions.Logging.Abstractions;
    using NEventStore;
    using NEventStore.Persistence;
    using NEventStore.Persistence.AcceptanceTests.BDD;
    using Xunit;

    public class PipelineHooksAwarePersistenceDecoratorTests
    {
        public class WhenDisposingTheDecorator : UsingUnderlyingPersistence
        {
            protected override Task Because()
            {
                Decorator.Dispose();

                return Task.CompletedTask;
            }

            [Fact]
            public void should_dispose_the_underlying_persistence()
            {
                A.CallTo(() => Persistence.Dispose()).MustHaveHappened(1, Times.Exactly);
            }
        }

        public class WhenReadingTheAllEventsFromDate : UsingUnderlyingPersistence
        {
            private ICommit _commit = null!;
            private DateTime _date;
            private IPipelineHook _hook1 = null!;
            private IPipelineHook _hook2 = null!;

            protected override Task Context()
            {
                _date = DateTime.Now;
                _commit = new Commit(
                    Bucket.Default,
                    UnderlyingStreamId,
                    1,
                    Guid.NewGuid(),
                    1,
                    DateTime.Now,
                    0,
                    null,
                    null);

                _hook1 = A.Fake<IPipelineHook>();
                A.CallTo(() => _hook1.Select(_commit)).Returns(_commit);
                PipelineHooks.Add(_hook1);

                _hook2 = A.Fake<IPipelineHook>();
                A.CallTo(() => _hook2.Select(_commit)).Returns(_commit);
                PipelineHooks.Add(_hook2);

                A.CallTo(() => Persistence.GetFrom(Bucket.Default, _date, CancellationToken.None))
                    .Returns(new List<ICommit> { _commit }.ToAsyncEnumerable(CancellationToken.None));

                return Task.CompletedTask;
            }

            protected override Task Because()
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                // Forces enumeration of commits.
                _ = Decorator.GetFrom(_date).ToList().ConfigureAwait(false);
                return Task.CompletedTask;
            }

            [Fact]
            public void should_call_the_underlying_persistence_to_get_events()
            {
                A.CallTo(() => Persistence.GetFrom(Bucket.Default, _date, CancellationToken.None))
                    .MustHaveHappened(1, Times.Exactly);
            }

            [Fact]
            public void should_pass_all_events_through_the_pipeline_hooks()
            {
                A.CallTo(() => _hook1.Select(_commit)).MustHaveHappened(1, Times.Exactly);
                A.CallTo(() => _hook2.Select(_commit)).MustHaveHappened(1, Times.Exactly);
            }
        }

        public class WhenGettingTheAllEventsFromMinToMaxRevision : UsingUnderlyingPersistence
        {
            private ICommit _commit = null!;
            private IPipelineHook _hook1 = null!;
            private IPipelineHook _hook2 = null!;

            protected override Task Context()
            {
                _commit = new Commit(
                    Bucket.Default,
                    UnderlyingStreamId,
                    1,
                    Guid.NewGuid(),
                    1,
                    DateTime.Now,
                    0,
                    null,
                    null);

                _hook1 = A.Fake<IPipelineHook>();
                A.CallTo(() => _hook1.Select(_commit)).Returns(_commit);
                PipelineHooks.Add(_hook1);

                _hook2 = A.Fake<IPipelineHook>();
                A.CallTo(() => _hook2.Select(_commit)).Returns(_commit);
                PipelineHooks.Add(_hook2);

                A.CallTo(
                        () => Persistence.GetFrom(
                            Bucket.Default,
                            _commit.StreamId,
                            0,
                            int.MaxValue,
                            CancellationToken.None))
                    .Returns(new List<ICommit> { _commit }.ToAsyncEnumerable(CancellationToken.None));

                return Task.CompletedTask;
            }

            protected override async Task Because()
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                // Forces enumeration of commits.
                _ = await Decorator.GetFrom(Bucket.Default, _commit.StreamId, 0, int.MaxValue)
                    .ToList()
                    .ConfigureAwait(false);
            }

            [Fact]
            public void should_call_the_underlying_persistence_to_get_events()
            {
                A.CallTo(
                        () => Persistence.GetFrom(
                            Bucket.Default,
                            _commit.StreamId,
                            0,
                            int.MaxValue,
                            CancellationToken.None))
                    .MustHaveHappened(1, Times.Exactly);
            }

            [Fact]
            public void should_pass_all_events_through_the_pipeline_hooks()
            {
                A.CallTo(() => _hook1.Select(_commit)).MustHaveHappened(1, Times.Exactly);
                A.CallTo(() => _hook2.Select(_commit)).MustHaveHappened(1, Times.Exactly);
            }
        }

        public class WhenGettingAllEventsFromTo : UsingUnderlyingPersistence
        {
            private ICommit _commit = null!;
            private DateTime _end;
            private IPipelineHook _hook1 = null!;
            private IPipelineHook _hook2 = null!;
            private DateTime _start;

            protected override Task Context()
            {
                _start = DateTime.Now;
                _end = DateTime.Now;
                _commit = new Commit(
                    Bucket.Default,
                    UnderlyingStreamId,
                    1,
                    Guid.NewGuid(),
                    1,
                    DateTime.Now,
                    0,
                    null,
                    null);

                _hook1 = A.Fake<IPipelineHook>();
                A.CallTo(() => _hook1.Select(_commit)).Returns(Task.FromResult(_commit));
                PipelineHooks.Add(_hook1);

                _hook2 = A.Fake<IPipelineHook>();
                A.CallTo(() => _hook2.Select(_commit)).Returns(Task.FromResult(_commit));
                PipelineHooks.Add(_hook2);

                A.CallTo(() => Persistence.GetFromTo(Bucket.Default, _start, _end, CancellationToken.None))
                    .Returns(new List<ICommit> { _commit }.ToAsyncEnumerable(CancellationToken.None));

                return Task.CompletedTask;
            }

            protected override async Task Because()
            {
                _ = await Decorator.GetFromTo(_start, _end, CancellationToken.None).ToList().ConfigureAwait(false);
            }

            [Fact]
            public void should_call_the_underlying_persistence_to_get_events()
            {
                A.CallTo(() => Persistence.GetFromTo(Bucket.Default, _start, _end, CancellationToken.None))
                    .MustHaveHappened(1, Times.Exactly);
            }

            [Fact]
            public void should_pass_all_events_through_the_pipeline_hooks()
            {
                A.CallTo(() => _hook1.Select(_commit)).MustHaveHappened(1, Times.Exactly);
                A.CallTo(() => _hook2.Select(_commit)).MustHaveHappened(1, Times.Exactly);
            }
        }

        public class WhenCommitting : UsingUnderlyingPersistence
        {
            private CommitAttempt _attempt = null!;

            protected override Task Context()
            {
                _attempt = new CommitAttempt(
                    Bucket.Default,
                    UnderlyingStreamId,
                    1,
                    Guid.NewGuid(),
                    1,
                    DateTime.Now,
                    null,
                    new List<EventMessage> { new EventMessage(new object()) });

                return Task.CompletedTask;
            }

            protected override async Task Because()
            {
                await Decorator.Commit(_attempt).ConfigureAwait(false);
            }

            [Fact]
            public void should_dispose_the_underlying_persistence()
            {
                A.CallTo(() => Persistence.Commit(_attempt)).MustHaveHappened(1, Times.Exactly);
            }
        }

        public class WhenReadingTheAllEventsFromCheckpoint : UsingUnderlyingPersistence
        {
            private ICommit _commit = null!;
            private IPipelineHook _hook1 = null!;
            private IPipelineHook _hook2 = null!;

            protected override Task Context()
            {
                _commit = new Commit(
                    Bucket.Default,
                    UnderlyingStreamId,
                    1,
                    Guid.NewGuid(),
                    1,
                    DateTime.Now,
                    0,
                    null,
                    null);

                _hook1 = A.Fake<IPipelineHook>();
                A.CallTo(() => _hook1.Select(_commit)).Returns(_commit);
                PipelineHooks.Add(_hook1);

                _hook2 = A.Fake<IPipelineHook>();
                A.CallTo(() => _hook2.Select(_commit)).Returns(_commit);
                PipelineHooks.Add(_hook2);

                A.CallTo(() => Persistence.GetFrom(0, A.Dummy<CancellationToken>()))
                    .Returns(new List<ICommit> { _commit }.ToAsyncEnumerable());

                return Task.CompletedTask;
            }

            protected override async Task Because()
            {
                _ = await Decorator.GetFrom(0, A.Dummy<CancellationToken>())
                    .ToList(A.Dummy<CancellationToken>())
                    .ConfigureAwait(false);
            }

            [Fact]
            public void should_call_the_underlying_persistence_to_get_events()
            {
                A.CallTo(() => Persistence.GetFrom(0, CancellationToken.None)).MustHaveHappened(1, Times.Exactly);
            }

            [Fact]
            public void should_pass_all_events_through_the_pipeline_hooks()
            {
                A.CallTo(() => _hook1.Select(_commit)).MustHaveHappened(1, Times.Exactly);
                A.CallTo(() => _hook2.Select(_commit)).MustHaveHappened(1, Times.Exactly);
            }
        }

        public class WhenPurging : UsingUnderlyingPersistence
        {
            private IPipelineHook _hook = null!;

            protected override Task Context()
            {
                _hook = A.Fake<IPipelineHook>();
                PipelineHooks.Add(_hook);

                return Task.CompletedTask;
            }

            protected override Task Because() => Decorator.Purge();

            [Fact]
            public void should_call_the_pipeline_hook_purge()
            {
                A.CallTo(() => _hook.OnPurge(null)).MustHaveHappened(1, Times.Exactly);
            }
        }

        public class WhenPurgingABucket : UsingUnderlyingPersistence
        {
            private IPipelineHook _hook = null!;
            private const string BucketId = "Bucket";

            protected override Task Context()
            {
                _hook = A.Fake<IPipelineHook>();
                PipelineHooks.Add(_hook);

                return Task.CompletedTask;
            }

            protected override Task Because() => Decorator.Purge(BucketId);

            [Fact]
            public void should_call_the_pipeline_hook_purge()
            {
                A.CallTo(() => _hook.OnPurge(BucketId)).MustHaveHappened(1, Times.Exactly);
            }
        }

        public class WhenDeletingAStream : UsingUnderlyingPersistence
        {
            private IPipelineHook _hook = null!;
            private const string BucketId = "Bucket";
            private const string StreamId = "Stream";

            protected override Task Context()
            {
                _hook = A.Fake<IPipelineHook>();
                PipelineHooks.Add(_hook);

                return Task.CompletedTask;
            }

            protected override Task Because() => Decorator.DeleteStream(BucketId, StreamId);

            [Fact]
            public void should_call_the_pipeline_hook_purge()
            {
                A.CallTo(() => _hook.OnDeleteStream(BucketId, StreamId)).MustHaveHappened(1, Times.Exactly);
            }
        }

        public abstract class UsingUnderlyingPersistence : SpecificationBase
        {
            private PipelineHooksAwarePersistanceDecorator? _decorator;
            protected readonly IPersistStreams Persistence = A.Fake<IPersistStreams>();
            protected readonly List<IPipelineHook> PipelineHooks = new();
            protected readonly string UnderlyingStreamId = Guid.NewGuid().ToString();

            public UsingUnderlyingPersistence()
            {
                OnStart().Wait();
            }

            public PipelineHooksAwarePersistanceDecorator Decorator
            {
                get
                {
                    return _decorator ??= new PipelineHooksAwarePersistanceDecorator(
                        Persistence,
                        PipelineHooks.Select(x => x),
                        NullLogger.Instance);
                }
            }
        }
    }
}
