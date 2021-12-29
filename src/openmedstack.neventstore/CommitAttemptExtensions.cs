using OpenMedStack.NEventStore.Persistence;

namespace OpenMedStack.NEventStore
{
    internal static class CommitAttemptExtensions
    {
        public static ICommit ToCommit(this CommitAttempt attempt, long checkpointToken) =>
            new Commit(
                attempt.BucketId,
                attempt.StreamId,
                attempt.StreamRevision,
                attempt.CommitId,
                attempt.CommitSequence,
                attempt.CommitStamp,
                checkpointToken,
                attempt.Headers,
                attempt.Events);
    }
}