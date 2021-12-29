namespace OpenMedStack.NEventStore.Tests.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.Extensions.Logging.Abstractions;
    using NEventStore;
    using NEventStore.Persistence.AcceptanceTests;
    using NEventStore.Persistence.AcceptanceTests.BDD;
    using openmedstack.neventstore.client;
    using Xunit;
    
    public class BaseHandlingCommittedEvents : UsingPollingClient2
    {
        private readonly List<ICommit> _commits = new();

        protected override Task Context()
        {
            base.Context();
            HandleFunction = c =>
            {
                _commits.Add(c);
                return Task.FromResult(PollingClient2.HandlingResult.MoveToNext);
            };
            StoreEvents.Advanced.CommitSingle();

            return Task.CompletedTask;
        }

        protected override Task Because()
        {
            Sut.StartFrom();

            return Task.CompletedTask;
        }

        [Fact]
        public void commits_are_correctly_dispatched()
        {
            WaitForCondition(() => _commits.Count >= 1);
            _commits.Count.Should().Be(1);
        }
    }

    public class BaseHandlingCommittedEventsAndNewEvents : UsingPollingClient2
    {
        private readonly List<ICommit> _commits = new();

        protected override Task Context()
        {
            base.Context();
            HandleFunction = c =>
            {
                _commits.Add(c);
                return Task.FromResult(PollingClient2.HandlingResult.MoveToNext);
            };
            StoreEvents.Advanced.CommitSingle();

            return Task.CompletedTask;
        }

        protected override Task Because()
        {
            Sut.StartFrom();
            for (var i = 0; i < 15; i++)
            {
                StoreEvents.Advanced.CommitSingle();
            }

            return Task.CompletedTask;
        }

        [Fact]
        public void commits_are_correctly_dispatched()
        {
            WaitForCondition(() => _commits.Count >= 16);
            _commits.Count.Should().Be(16);
        }
    }

    public class VerifyStoppingCommitPollingClient : UsingPollingClient2
    {
        private readonly List<ICommit> _commits = new();

        protected override async Task Context()
        {
            await base.Context().ConfigureAwait(false);
            HandleFunction = c =>
            {
                _commits.Add(c);
                return Task.FromResult(PollingClient2.HandlingResult.Stop);
            };
            await StoreEvents.Advanced.CommitSingle().ConfigureAwait(false);
            await StoreEvents.Advanced.CommitSingle().ConfigureAwait(false);
            await StoreEvents.Advanced.CommitSingle().ConfigureAwait(false);
        }

        protected override Task Because()
        {
            Sut.StartFrom();

            return Task.CompletedTask;
        }

        [Fact]
        public void commits_are_correctly_dispatched()
        {
            WaitForCondition(() => _commits.Count >= 2, timeoutInSeconds: 1);
            _commits.Count.Should().Be(1);
        }
    }

    public class VerifyRetryCommitPollingClient : UsingPollingClient2
    {
        private readonly List<ICommit> _commits = new();

        protected override Task Context()
        {
            base.Context();
            HandleFunction = c =>
            {
                _commits.Add(c);
                if (_commits.Count < 3)
                {
                    return Task.FromResult(PollingClient2.HandlingResult.Retry);
                }

                return Task.FromResult(PollingClient2.HandlingResult.MoveToNext);
            };
            StoreEvents.Advanced.CommitSingle();

            return Task.CompletedTask;
        }

        protected override Task Because()
        {
            Sut.StartFrom();

            return Task.CompletedTask;
        }

        [Fact]
        public void commits_are_retried()
        {
            WaitForCondition(() => _commits.Count >= 3, timeoutInSeconds: 1);
            _commits.Count.Should().Be(3);
            _commits.All(c => c.CheckpointToken == 1).Should().BeTrue();
        }
    }

    public class VerifyRetryThenMoveNext : UsingPollingClient2
    {
        private readonly List<ICommit> _commits = new();

        protected override Task Context()
        {
            base.Context();
            HandleFunction = c =>
            {
                _commits.Add(c);
                return Task.FromResult(_commits.Count < 3 && c.CheckpointToken == 1
                    ? PollingClient2.HandlingResult.Retry
                    : PollingClient2.HandlingResult.MoveToNext);
            };
            StoreEvents.Advanced.CommitSingle();
            StoreEvents.Advanced.CommitSingle();

            return Task.CompletedTask;
        }

        protected override Task Because()
        {
            Sut.StartFrom();

            return Task.CompletedTask;
        }

        [Fact]
        public void commits_are_retried_then_move_next()
        {
            WaitForCondition(() => _commits.Count >= 4, timeoutInSeconds: 1);
            _commits.Count.Should().Be(4);
            _commits
                .Select(c => c.CheckpointToken)
                .SequenceEqual(new[] { 1L, 1L, 1, 2 })
                .Should().BeTrue();
        }
    }

    public class VerifyManualPlling : UsingPollingClient2
    {
        private readonly List<ICommit> _commits = new();

        protected override Task Context()
        {
            base.Context();
            HandleFunction = c =>
            {
                _commits.Add(c);
                return Task.FromResult(PollingClient2.HandlingResult.MoveToNext);
            };
            StoreEvents.Advanced.CommitSingle();
            StoreEvents.Advanced.CommitSingle();

            return Task.CompletedTask;
        }

        protected override Task Because()
        {
            Sut.ConfigurePollingFunction();
            Sut.PollNow();

            return Task.CompletedTask;
        }

        [Fact]
        public void commits_are_retried_then_move_next()
        {
            WaitForCondition(() => _commits.Count >= 2, timeoutInSeconds: 3);
            _commits.Count.Should().Be(2);
            _commits
                .Select(c => c.CheckpointToken)
                .SequenceEqual(new[] { 1L, 2L })
                .Should().BeTrue();
        }
    }

    public abstract class UsingPollingClient2 : SpecificationBase
    {
        private readonly TimeSpan _pollingInterval = TimeSpan.FromMilliseconds(100);

        public UsingPollingClient2()
        {
            OnStart().Wait();
        }

        protected PollingClient2 Sut { get; private set; } = null!;

        protected IStoreEvents StoreEvents { get; private set; } = null!;

        protected Func<ICommit, Task<PollingClient2.HandlingResult>> HandleFunction = null!;

        protected override Task Context()
        {
            HandleFunction = c => Task.FromResult(PollingClient2.HandlingResult.MoveToNext);
            StoreEvents = Wireup.Init(NullLogger.Instance).UsingInMemoryPersistence().Build();
            Sut = new PollingClient2(StoreEvents.Advanced, c => HandleFunction(c), NullLogger.Instance, _pollingInterval);
            return Task.CompletedTask;
        }

        protected override void Cleanup()
        {
            StoreEvents.Dispose();
            Sut.Dispose();
        }

        protected static void WaitForCondition(Func<bool> predicate, int timeoutInSeconds = 4)
        {
            var startTest = DateTime.Now;
            while (!predicate() && DateTime.Now.Subtract(startTest).TotalSeconds < timeoutInSeconds)
            {
                Thread.Sleep(100);
            }
        }
    }
}
