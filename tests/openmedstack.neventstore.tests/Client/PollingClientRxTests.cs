using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Tests.Client;

using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using NEventStore;
using NEventStore.Persistence.AcceptanceTests;
using NEventStore.Persistence.AcceptanceTests.BDD;
using Xunit;

public class CreatingPollingClientTests
{
    [Fact]
    public void When_interval_less_than_zero_then_should_throw()
    {
        Assert.Throws<ArgumentException>(
            () => { _ = new PollingClientRx(Substitute.For<IManagePersistence>(), TimeSpan.MinValue); });
    }
}

public class WhenCommitIsCommittedBeforeSubscribing : UsingPollingClient
{
    private IObservable<ICommit> _observeCommits = null!;
    private Task<ICommit> _commitObserved = null!;

    protected override Task Context()
    {
        base.Context();
        StoreEvents.CommitSingle();
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
    public async Task should_observe_commit()
    {
        await Task.Delay(PollingInterval * 10);
        Assert.True(_commitObserved.IsCompleted);
    }
}

public class WhenCommitIsCommittedBeforeAndAfterSubscribing : UsingPollingClient
{
    private IObservable<ICommit> _observeCommits = null!;
    private Task<ICommit> _twoCommitsObserved = null!;

    protected override Task Context()
    {
        base.Context();
        StoreEvents.CommitSingle();
        _observeCommits = PollingClient.ObserveFrom();
        _twoCommitsObserved = _observeCommits.Take(2).ToTask();

        return Task.CompletedTask;
    }

    protected override Task Because()
    {
        PollingClient.Start();
        StoreEvents.CommitSingle();

        return Task.CompletedTask;
    }

    protected override void Cleanup()
    {
        PollingClient.Dispose();
    }

    [Fact]
    public async Task should_observe_two_commits()
    {
        await Task.Delay(PollingInterval * 10);
        Assert.True(_twoCommitsObserved.IsCompleted);
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
        StoreEvents.CommitSingle();
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
                    StoreEvents.CommitSingle();
                }
            });

        return Task.CompletedTask;
    }

    protected override void Cleanup()
    {
        PollingClient.Dispose();
    }

    [Fact]
    public async Task should_observe_commits_on_first_observer()
    {
        await Task.Delay(PollingInterval * 10);
        Assert.True(_observeCommits1Complete.IsCompleted);
    }

    [Fact]
    public async Task should_observe_commits_on_second_observer()
    {
        await Task.Delay(PollingInterval * 10);
        Assert.True(_observeCommits2Complete.IsCompleted);
    }
}

public class WhenPollingFromBucket1 : UsingPollingClient
{
    private IObservable<ICommit> _observeCommits = null!;
    private Task<ICommit> _commitObserved = null!;

    protected override async Task Context()
    {
        await base.Context();
        await StoreEvents.CommitMany(4, null, "bucket_2");
        await StoreEvents.CommitMany(4, null, "bucket_1");
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
    public async Task should_observe_commit_from_bucket1()
    {
        var result = await _commitObserved;
        //Assert.True(_commitObserved.Wait(PollingInterval * 2));
        Assert.Equal("bucket_1", result.TenantId);
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

    protected ICommitEvents StoreEvents { get; private set; } = null!;

    protected override Task Context()
    {
        var collection = new ServiceCollection().RegisterInMemoryEventStore().RegisterJsonSerialization().AddLogging();

        var serviceProvider = collection.BuildServiceProvider();
        StoreEvents = serviceProvider.GetRequiredService<ICommitEvents>();
        PollingClient = new PollingClientRx(serviceProvider.GetRequiredService<IManagePersistence>(), PollingInterval);
        return Task.CompletedTask;
    }

    protected override void Cleanup()
    {
    }
}
