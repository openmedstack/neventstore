namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NEventStore;
    using Persistence;

    public static class ExtensionMethods
    {
        public static Task<ICommit?> CommitSingle(this IPersistStreams persistence, string? streamId = null)
        {
            var commitAttempt = (streamId ?? Guid.NewGuid().ToString()).BuildAttempt();
            return persistence.Commit(commitAttempt);
        }

        public static Task<ICommit?> CommitNext(this IPersistStreams persistence, ICommit previous)
        {
            var nextAttempt = previous.BuildNextAttempt();
            return persistence.Commit(nextAttempt);
        }

        public static Task<ICommit?> CommitNext(this IPersistStreams persistence, CommitAttempt previous)
        {
            var nextAttempt = previous.BuildNextAttempt();
            return persistence.Commit(nextAttempt);
        }

        public static async Task<List<CommitAttempt>> CommitMany(this IPersistStreams persistence, int numberOfCommits, string? streamId = null, string? bucketId = null)
        {
            var commits = new List<CommitAttempt>();
            CommitAttempt? attempt = null;

            for (var i = 0; i < numberOfCommits; i++)
            {
                attempt = attempt == null ? (streamId ?? Guid.NewGuid().ToString()).BuildAttempt(null, bucketId) : attempt.BuildNextAttempt();
                await persistence.Commit(attempt).ConfigureAwait(false);
                commits.Add(attempt);
            }

            return commits;
        }

        public static CommitAttempt BuildAttempt(this string streamId, DateTimeOffset? now = null, string? bucketId = null)
        {
            now ??= SystemTime.UtcNow;
            bucketId ??= Bucket.Default;

            var messages = new List<EventMessage>
            {
                new EventMessage ( new SomeDomainEvent {SomeProperty = "Test"}),
                new EventMessage ( new SomeDomainEvent {SomeProperty = "Test2"}),
            };

            return new CommitAttempt(
                bucketId: bucketId,
                streamId: streamId,
                streamRevision: 2,
                commitId: Guid.NewGuid(),
                commitSequence: 1,
                commitStamp: now.Value,
                headers: new Dictionary<string, object> { { "A header", "A string value" }, { "Another header", 2 } },
                events: messages
            );
        }

        public static CommitAttempt BuildNextAttempt(this ICommit commit)
        {
            var messages = new List<EventMessage>
            {
                new EventMessage(new SomeDomainEvent {SomeProperty = "Another test"}),
                new EventMessage(new SomeDomainEvent {SomeProperty = "Another test2"})
            };

            return new CommitAttempt(commit.BucketId,
                commit.StreamId,
                commit.StreamRevision + messages.Count,
                Guid.NewGuid(),
                commit.CommitSequence + 1,
                commit.CommitStamp.AddSeconds(1),
                new Dictionary<string, object>(),
                messages);
        }

        public static CommitAttempt BuildNextAttempt(this CommitAttempt commit)
        {
            var messages = new List<EventMessage>
            {
                new EventMessage ( new SomeDomainEvent {SomeProperty = "Another test"}),
                new EventMessage ( new SomeDomainEvent {SomeProperty = "Another test2"})
            };

            return new CommitAttempt(commit.BucketId,
                commit.StreamId,
                commit.StreamRevision + 2,
                Guid.NewGuid(),
                commit.CommitSequence + 1,
                commit.CommitStamp.AddSeconds(1),
                new Dictionary<string, object>(),
                messages);
        }

        [Serializable]
        public class SomeDomainEvent
        {
            public string SomeProperty { get; set; } = null!;

            public override string ToString() => SomeProperty;
        }
    }
}