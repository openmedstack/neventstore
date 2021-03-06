namespace OpenMedStack.NEventStore.Tests.Client
{
    using System;
    using System.Reactive.Linq;
    using System.Reactive.Threading.Tasks;
    using System.Threading.Tasks;
    using FakeItEasy;
    using FluentAssertions;
    using Microsoft.Extensions.Logging.Abstractions;
    using NEventStore;
    using NEventStore.Persistence;
    using NEventStore.Persistence.AcceptanceTests;
    using NEventStore.Persistence.AcceptanceTests.BDD;
    using Xunit;

    public class CreatingPollingClientTests
    {
        //[Fact]
        //public void When_persist_streams_is_null_then_should_throw()
        //{
        //    Catch.Exception(() => new PollingClientRx(null)).Should().BeOfType<ArgumentNullException>();
        //}

        [Fact]
        public void When_interval_less_than_zero_then_should_throw()
        {
            Catch.Exception(
                    () =>
                    {
                        var x = new PollingClientRx(A.Fake<IPersistStreams>(), TimeSpan.MinValue);
                    })!
                .Should()
                .BeOfType<ArgumentException>();
        }
    }

    public class WhenCommitIsComittedBeforeSubscribing : UsingPollingClient
    {
        private IObservable<ICommit> _observeCommits = null!;
        private Task<ICommit> _commitObserved = null!;

        protected override Task Context()
        {
            base.Context();
            StoreEvents.Advanced.CommitSingle();
            _observeCommits = PollingClient.ObserveFrom();
            _commitObserved = _observeCommits.FirstAsync().ToTask();

            return Task.CompletedTask;
        }

        protected override Task Because()
        {
            PollingClient.Start();

            return Task.CompletedTask;
        }

        protected override void Cleanup()
        {
            PollingClient.Dispose();
        }

        [Fact]
        public void should_observe_commit()
        {
            _commitObserved.Wait(PollingInterval * 2).Should().BeTrue();
        }
    }

    public class WhenCommitIsComittedBeforeAndAfterSubscribing : UsingPollingClient
    {
        private IObservable<ICommit> _observeCommits = null!;
        private Task<ICommit> _twoCommitsObserved = null!;

        protected override Task Context()
        {
            base.Context();
            StoreEvents.Advanced.CommitSingle();
            _observeCommits = PollingClient.ObserveFrom();
            _twoCommitsObserved = _observeCommits.Take(2).ToTask();

            return Task.CompletedTask;
        }

        protected override Task Because()
        {
            PollingClient.Start();
            StoreEvents.Advanced.CommitSingle();

            return Task.CompletedTask;
        }

        protected override void Cleanup()
        {
            PollingClient.Dispose();
        }

        [Fact]
        public void should_observe_two_commits()
        {
            _twoCommitsObserved.Wait(PollingInterval * 2).Should().BeTrue();
        }
    }

    public class WithTwoSubscriptionsOnASingleObserverAndMultipleCommits : UsingPollingClient
    {
        private IObservable<ICommit> _observeCommits1 = null!;
        private Task<ICommit> _observeCommits1Complete = null!;
        private Task<ICommit> _observeCommits2Complete = null!;

        protected override Task Context()
        {
            base.Context();
            StoreEvents.Advanced.CommitSingle();
            _observeCommits1 = PollingClient.ObserveFrom();
            _observeCommits1Complete = _observeCommits1.Take(5).ToTask();
            _observeCommits2Complete = _observeCommits1.Take(10).ToTask();

            return Task.CompletedTask;
        }

        protected override Task Because()
        {
            PollingClient.Start();
            Task.Factory.StartNew(
                () =>
                {
                    for (var i = 0; i < 15; i++)
                    {
                        StoreEvents.Advanced.CommitSingle();
                    }
                });

            return Task.CompletedTask;
        }

        protected override void Cleanup()
        {
            PollingClient.Dispose();
        }

        [Fact]
        public void should_observe_commits_on_first_observer()
        {
            _observeCommits1Complete.Wait(PollingInterval * 10).Should().BeTrue();
        }

        [Fact]
        public void should_observe_commits_on_second_observer()
        {
            _observeCommits2Complete.Wait(PollingInterval * 10).Should().BeTrue();
        }
    }

    public class WhenPollingFromBucket1 : UsingPollingClient
    {
        private IObservable<ICommit> _observeCommits = null!;
        private Task<ICommit> _commitObserved = null!;

        protected override async Task Context()
        {
            await base.Context().ConfigureAwait(false);
            await StoreEvents.Advanced.CommitMany(4, null, "bucket_2").ConfigureAwait(false);
            await StoreEvents.Advanced.CommitMany(4, null, "bucket_1").ConfigureAwait(false);
            _observeCommits = PollingClient.ObserveFrom();
            _commitObserved = _observeCommits.FirstAsync().ToTask();
        }

        protected override Task Because()
        {
            PollingClient.StartFromBucket("bucket_1");

            return Task.CompletedTask;
        }

        protected override void Cleanup()
        {
            PollingClient.Dispose();
        }

        [Fact]
        public void should_observe_commit_from_bucket1()
        {
            _commitObserved.Wait(PollingInterval * 2).Should().BeTrue();
            _commitObserved.Result.BucketId.Should().Be("bucket_1");
        }
    }

    public abstract class UsingPollingClient : SpecificationBase
    {
        protected readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(100);

        public UsingPollingClient()
        {
            OnStart().Wait();
        }

        protected PollingClientRx PollingClient { get; private set; } = null!;

        protected IStoreEvents StoreEvents { get; private set; } = null!;

        protected override Task Context()
        {
            StoreEvents = Wireup.Init(NullLogger.Instance).UsingInMemoryPersistence().Build();
            PollingClient = new PollingClientRx(StoreEvents.Advanced, PollingInterval);
            return Task.CompletedTask;
        }

        protected override void Cleanup()
        {
            StoreEvents.Dispose();
        }
    }
}
