using Amazon.DynamoDBv2.DataModel;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.DynamoDb;

[DynamoDBTable("commits", LowerCamelCaseProperties = false)]
internal class DynamoDbCommit
{
    [DynamoDBHashKey] public required string BucketAndStream { get; set; }
    [DynamoDBProperty] public required string BucketId { get; set; }
    [DynamoDBProperty] public required string StreamId { get; set; }
    [DynamoDBLocalSecondaryIndexRangeKey] public int StreamRevision { get; set; }
    [DynamoDBProperty] public required string CommitId { get; set; }
    [DynamoDBRangeKey] public int CommitSequence { get; set; }
    [DynamoDBProperty] public long CommitStamp { get; set; }
    [DynamoDBProperty] public byte[] Headers { get; set; } = Array.Empty<byte>();
    [DynamoDBProperty] public byte[] Events { get; set; } = Array.Empty<byte>();

    public static DynamoDbCommit FromStream(IEventStream eventStream, Guid? commitId, ISerialize serializer)
    {
        return new DynamoDbCommit
        {
            BucketAndStream = $"{eventStream.BucketId}{eventStream.StreamId}",
            BucketId = eventStream.BucketId,
            StreamId = eventStream.StreamId,
            StreamRevision = eventStream.StreamRevision,
            CommitId = (commitId ?? Guid.NewGuid()).ToString("N"),
            CommitSequence = eventStream.CommitSequence + 1,
            CommitStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Headers = serializer.Serialize(eventStream.UncommittedHeaders),
            Events = serializer.Serialize(eventStream.UncommittedEvents)
        };
    }
}
