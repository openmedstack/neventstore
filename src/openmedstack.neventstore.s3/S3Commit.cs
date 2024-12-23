using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.S3;

internal class S3Commit
{
    public required string TenantId { get; set; }
    public required string StreamId { get; set; }
    public int StreamRevision { get; set; }
    public required string CommitId { get; set; }
    public int CommitSequence { get; set; }
    public long CommitStamp { get; set; }
    public byte[] Headers { get; set; } = [];
    public byte[] Events { get; set; } = [];

    public static S3Commit FromCommitAttempt(CommitAttempt commitAttempt, ISerialize serializer)
    {
        return new S3Commit
        {
            TenantId = commitAttempt.TenantId,
            StreamId = commitAttempt.StreamId,
            StreamRevision = commitAttempt.StreamRevision,
            CommitId = commitAttempt.CommitId.ToString("N"),
            CommitSequence = commitAttempt.CommitSequence + 1,
            CommitStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Headers = serializer.Serialize(commitAttempt.Headers),
            Events = serializer.Serialize(commitAttempt.Events)
        };
    }

    public ICommit ToCommit(ISerialize serializer)
    {
        return new Commit(TenantId, StreamId, StreamRevision, Guid.Parse(CommitId), CommitSequence,
            DateTimeOffset.FromUnixTimeSeconds(CommitStamp), 0,
            serializer.Deserialize<Dictionary<string, object>>(Headers),
            serializer.Deserialize<List<EventMessage>>(Events));
    }
}
