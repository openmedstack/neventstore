using Amazon.DynamoDBv2.DataModel;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.DynamoDb;

[DynamoDBTable("commits", LowerCamelCaseProperties = false)]
internal class DynamoDbCommit
{
    [DynamoDBHashKey] public required string TenantAndStream { get; set; }
    [DynamoDBProperty] public required string TenantId { get; set; }
    [DynamoDBProperty] public required string StreamId { get; set; }
    [DynamoDBLocalSecondaryIndexRangeKey] public int StreamRevision { get; set; }
    [DynamoDBProperty] public required string CommitId { get; set; }
    [DynamoDBRangeKey] public int CommitSequence { get; set; }
    [DynamoDBProperty] public long CommitStamp { get; set; }
    [DynamoDBProperty] public byte[] Headers { get; set; } = [];
    [DynamoDBProperty] public byte[] Events { get; set; } = [];

    public static DynamoDbCommit FromCommitAttempt(CommitAttempt commitAttempt, ISerialize serializer)
    {
        return new DynamoDbCommit
        {
            TenantAndStream = $"{commitAttempt.TenantId}{commitAttempt.StreamId}",
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
}
