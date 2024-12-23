﻿using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Persistence.AcceptanceTests;

namespace OpenMedStack.NEventStore.Tests.Persistence.InMemory;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NEventStore.Persistence.AcceptanceTests.BDD;
using NEventStore.Persistence.InMemory;
using Xunit;

public class WhenGettingFromToThenShouldNotGetLaterCommits : SpecificationBase
{
    private readonly DateTime _endDate = DateTime.UtcNow.Date.AddDays(1); // new(2013, 1, 2);
    private readonly DateTime _startDate = DateTime.UtcNow.Date; //new(2013, 1, 1);
    private ICommit[] _commits = null!;
    private InMemoryPersistenceEngine _engine = null!;

    public WhenGettingFromToThenShouldNotGetLaterCommits()
    {
        OnStart().Wait();
    }

    protected override async Task Context()
    {
        _engine = new InMemoryPersistenceEngine(NullLogger<InMemoryPersistenceEngine>.Instance);
        await _engine.Initialize().ConfigureAwait(false);
        var streamId = Guid.NewGuid().ToString();
        await _engine.Commit(new CommitAttempt(
            Bucket.Default,
            streamId,
            1,
            Guid.NewGuid(),
            1,
            _startDate,
            new Dictionary<string, object>(),
            new List<EventMessage> { new EventMessage(new object()) })).ConfigureAwait(false);
        await _engine.Commit(
            new CommitAttempt(
                Bucket.Default,
                streamId,
                2,
                Guid.NewGuid(),
                2,
                _endDate,
                new Dictionary<string, object>(),
                new List<EventMessage> { new EventMessage(new object()) })).ConfigureAwait(false);
    }

    protected override async Task Because()
    {
        var fromTo = _engine.GetFromTo(Bucket.Default, _startDate, _endDate, CancellationToken.None);
        _commits = await fromTo.ToArray().ConfigureAwait(false);
    }

    [Fact]
    public void should_return_two_commits()
    {
        Assert.Equal(2, _commits.Length);
    }
}
