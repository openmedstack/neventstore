using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public static class ExtensionMethods
{
    public static Task<ICommit?> CommitSingle(this ICommitEvents persistence, string? streamId = null)
    {
        var commitAttempt = (streamId ?? Guid.NewGuid().ToString()).BuildAttempt();
        return persistence.Commit(commitAttempt);
    }

    public static async Task<ICommit?> CommitNext(this ICommitEvents persistence, ICommit previous)
    {
        var nextAttempt = previous.BuildNextAttempt();
        return await persistence.Commit(nextAttempt).ConfigureAwait(false);
    }

    public static async Task<List<ICommit>> CommitMany(
        this ICommitEvents persistence,
        int numberOfCommits,
        string? streamId = null,
        string? TenantId = null)
    {
        var commits = new List<ICommit>();
        CommitAttempt? attempt = null;

        for (var i = 0; i < numberOfCommits; i++)
        {
            attempt = attempt == null
                ? (streamId ?? Guid.NewGuid().ToString()).BuildAttempt(TenantId)
                : attempt.BuildNextAttempt();
            var commit = await persistence.Commit(attempt)
                .ConfigureAwait(false);
            commits.Add(commit!);
        }

        return commits;
    }

    public static CommitAttempt BuildAttempt(
        this string streamId,
        string? TenantId = null)
    {
        TenantId ??= "default";

        var stream = new CommitAttempt(TenantId, streamId, 2, Guid.NewGuid(), 1, DateTimeOffset.UtcNow,
            new Dictionary<string, object>
            {
                ["A header"] = "A string value",
                ["Another header"] = 2
            },
            [
                new EventMessage(new SomeDomainEvent { SomeProperty = "Test" }),
                new EventMessage(new SomeDomainEvent { SomeProperty = "Test2" })
            ]);
        return stream;
    }

    public static CommitAttempt BuildNextAttempt(this CommitAttempt stream)
    {
        var attempt = new CommitAttempt(stream.TenantId, stream.StreamId, stream.StreamRevision + 2, Guid.NewGuid(),
            stream.CommitSequence + 1, DateTimeOffset.UtcNow,
            new Dictionary<string, object>
            {
                ["A header"] = "A string value",
                ["Another header"] = 2
            },
            [
                new EventMessage(new SomeDomainEvent { SomeProperty = "Another test" }),
                new EventMessage(new SomeDomainEvent { SomeProperty = "Another test2" })
            ]);
        return attempt;
    }

    public static CommitAttempt BuildNextAttempt(this ICommit commit)
    {
        return new CommitAttempt(commit.TenantId, commit.StreamId, commit.StreamRevision + 2, Guid.NewGuid(),
            commit.CommitSequence + 1, DateTimeOffset.UtcNow,
            new Dictionary<string, object>
            {
                ["A header"] = "A string value",
                ["Another header"] = 2
            },
            [
                new EventMessage(new SomeDomainEvent { SomeProperty = "Another test" }),
                new EventMessage(new SomeDomainEvent { SomeProperty = "Another test2" })
            ]);
    }

    public class SomeDomainEvent
    {
        public string SomeProperty { get; set; } = null!;

        public override string ToString() => SomeProperty;
    }
}
