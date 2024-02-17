using Microsoft.Extensions.DependencyInjection;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.Tests.Client;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NEventStore;
using NEventStore.Persistence.AcceptanceTests;
using NEventStore.Persistence.AcceptanceTests.BDD;
using PollingClient;
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
            return Task.FromResult(HandlingResult.MoveToNext);
        };
        StoreEvents.CommitSingle();

        return Task.CompletedTask;
    }

    protected override Task Because()
    {
        Sut.StartFromBucket(Tests.Bucket.Default);

        return Task.CompletedTask;
    }

    [Fact]
    public void commits_are_correctly_dispatched()
    {
        WaitForCondition(() => _commits.Count >= 1);
        Assert.Single(_commits);
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
            return Task.FromResult(HandlingResult.MoveToNext);
        };
        StoreEvents.CommitSingle();

        return Task.CompletedTask;
    }

    protected override Task Because()
    {
        Sut.StartFromBucket(Tests.Bucket.Default);
        for (var i = 0; i < 15; i++)
        {
            StoreEvents.CommitSingle();
        }

        return Task.CompletedTask;
    }

    [Fact]
    public void commits_are_correctly_dispatched()
    {
        WaitForCondition(() => _commits.Count >= 16);
        Assert.Equal(16, _commits.Count);
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
            return Task.FromResult(HandlingResult.Stop);
        };
        await StoreEvents.CommitSingle().ConfigureAwait(false);
        await StoreEvents.CommitSingle().ConfigureAwait(false);
        await StoreEvents.CommitSingle().ConfigureAwait(false);
    }

    protected override Task Because()
    {
        Sut.StartFromBucket(Tests.Bucket.Default);

        return Task.CompletedTask;
    }

    [Fact]
    public void commits_are_correctly_dispatched()
    {
        WaitForCondition(() => _commits.Count >= 2, timeoutInSeconds: 1);
        Assert.Single(_commits);
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
                return Task.FromResult(HandlingResult.Retry);
            }

            return Task.FromResult(HandlingResult.MoveToNext);
        };
        StoreEvents.CommitSingle();

        return Task.CompletedTask;
    }

    protected override Task Because()
    {
        Sut.StartFromBucket(Tests.Bucket.Default);

        return Task.CompletedTask;
    }

    [Fact]
    public void commits_are_retried()
    {
        WaitForCondition(() => _commits.Count >= 3, timeoutInSeconds: 1);
        Assert.Equal(3, _commits.Count);
        Assert.All(_commits, c => Assert.Equal(1, c.CheckpointToken));
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
                ? HandlingResult.Retry
                : HandlingResult.MoveToNext);
        };
        StoreEvents.CommitSingle();
        StoreEvents.CommitSingle();

        return Task.CompletedTask;
    }

    protected override Task Because()
    {
        Sut.StartFromBucket(Tests.Bucket.Default);

        return Task.CompletedTask;
    }

    [Fact]
    public void commits_are_retried_then_move_next()
    {
        WaitForCondition(() => _commits.Count >= 4, timeoutInSeconds: 1);
        Assert.Equal(4, _commits.Count);
        Assert.Equal(new[] { 1L, 1L, 1, 2 }, _commits
            .Select(c => c.CheckpointToken));
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
            return Task.FromResult(HandlingResult.MoveToNext);
        };
        StoreEvents.CommitSingle();
        StoreEvents.CommitSingle();

        return Task.CompletedTask;
    }

    protected override Task Because()
    {
        Sut.ConfigurePollingFunction(Tests.Bucket.Default);
        Sut.PollNow();

        return Task.CompletedTask;
    }

    [Fact]
    public void commits_are_retried_then_move_next()
    {
        WaitForCondition(() => _commits.Count >= 2, timeoutInSeconds: 3);
        Assert.Equal(2, _commits.Count);
        Assert.Equal(new[] { 1L, 2L }, _commits
            .Select(c => c.CheckpointToken));
    }
}

public abstract class UsingPollingClient2 : SpecificationBase
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMilliseconds(100);

    public UsingPollingClient2()
    {
        OnStart().Wait();
    }

    protected PollingClient Sut { get; private set; } = null!;

    protected ICommitEvents StoreEvents { get; private set; } = null!;

    protected Func<ICommit, Task<HandlingResult>> HandleFunction = null!;

    protected override Task Context()
    {
        var collection = new ServiceCollection().RegisterInMemoryEventStore().RegisterJsonSerialization().AddLogging();
        var serviceProvider = collection.BuildServiceProvider();
        HandleFunction = _ => Task.FromResult(HandlingResult.MoveToNext);
        StoreEvents =
            serviceProvider
                .GetRequiredService<
                    ICommitEvents>(); //Wireup.Init(NullLoggerFactory.Instance).UsingInMemoryPersistence().Build();
        Sut = new PollingClient(serviceProvider.GetRequiredService<IManagePersistence>(), c => HandleFunction(c),
            NullLogger.Instance,
            _pollingInterval);
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
