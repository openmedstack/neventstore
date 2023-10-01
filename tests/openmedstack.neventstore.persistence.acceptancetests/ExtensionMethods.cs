using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public static class ExtensionMethods
{
    public static Task<ICommit?> CommitSingle(this IPersistStreams persistence, string? streamId = null)
    {
        var commitAttempt = (streamId ?? Guid.NewGuid().ToString()).BuildAttempt();
        return persistence.Commit(commitAttempt);
    }

    public static async Task<ICommit?> CommitNext(this IPersistStreams persistence, ICommit previous)
    {
        var nextAttempt = await previous.BuildNextAttempt();
        return await persistence.Commit(nextAttempt);
    }

    public static async Task<ICommit?> CommitNext(this IPersistStreams persistence, CommitAttempt previous)
    {
        var nextAttempt = await previous.BuildNextAttempt();
        return await persistence.Commit(nextAttempt);
    }

    public static async Task<List<ICommit>> CommitMany(
        this IPersistStreams persistence,
        int numberOfCommits,
        string? streamId = null,
        string? bucketId = null)
    {
        var commits = new List<ICommit>();
        IEventStream? attempt = null;

        for (var i = 0; i < numberOfCommits; i++)
        {
            attempt = attempt == null
                ? (streamId ?? Guid.NewGuid().ToString()).BuildAttempt(bucketId)
                : attempt.BuildNextAttempt();
            var commit = await persistence.Commit(attempt)
                .ConfigureAwait(false);
            commits.Add(commit!);
        }

        return commits;
    }

    public static IEventStream BuildAttempt(
        this string streamId,
        string? bucketId = null)
    {
        bucketId ??= Bucket.Default;

        var stream = OptimisticEventStream.Create(bucketId, streamId, NullLogger<OptimisticEventStream>.Instance);
        stream.Add(new EventMessage(new SomeDomainEvent { SomeProperty = "Test" }));
        stream.Add(new EventMessage(new SomeDomainEvent { SomeProperty = "Test2" }));
        stream.UncommittedHeaders.Add("A header", "A string value");
        stream.UncommittedHeaders.Add("Another header", 2);
        return stream;
    }

    public static IEventStream BuildNextAttempt(this IEventStream stream)
    {
        stream.SetPersisted(stream.CommitSequence + 1);
        stream.Add(new EventMessage(new SomeDomainEvent { SomeProperty = "Another test" }));
        stream.Add(new EventMessage(new SomeDomainEvent { SomeProperty = "Another test2" }));
        stream.UncommittedHeaders.Add("A header", "A string value");
        stream.UncommittedHeaders.Add("Another header", 2);
        return stream;
    }

    public static async Task<IEventStream> BuildNextAttempt(this CommitAttempt commit)
    {
        var stream = await OptimisticEventStream.Create(
            commit.BucketId,
            commit.StreamId,
            new[]
            {
                new Commit(commit.BucketId, commit.StreamId, commit.StreamRevision, commit.CommitId,
                    commit.CommitSequence, commit.CommitStamp, 0, commit.Headers, commit.Events)
            }.ToAsyncEnumerable(),
            commit.StreamRevision, commit.StreamRevision + commit.Events.Count);
        stream.Add(new EventMessage(new SomeDomainEvent { SomeProperty = "Another test" }));
        stream.Add(new EventMessage(new SomeDomainEvent { SomeProperty = "Another test2" }));
        return stream;
    }

    public static async Task<IEventStream> BuildNextAttempt(this ICommit commit)
    {
        var stream = await OptimisticEventStream.Create(
            commit.BucketId,
            commit.StreamId,
            new[]
            {
                commit
            }.ToAsyncEnumerable(),
            commit.StreamRevision, commit.StreamRevision + commit.Events.Count);
        stream.Add(new EventMessage(new SomeDomainEvent { SomeProperty = "Another test" }));
        stream.Add(new EventMessage(new SomeDomainEvent { SomeProperty = "Another test2" }));
        return stream;
    }

    [Serializable]
    public class SomeDomainEvent
    {
        public string SomeProperty { get; set; } = null!;

        public override string ToString() => SomeProperty;
    }
}
