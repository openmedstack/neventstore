using Amazon.DynamoDBv2.DataModel;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.DynamoDb;

[DynamoDBTable("commits", LowerCamelCaseProperties = false)]
internal class DynamoDbCommit
{
    [DynamoDBHashKey] public required string BucketAndStreamAndSequence { get; set; }
    [DynamoDBProperty] public required string BucketId { get; set; }
    [DynamoDBProperty] public required string StreamId { get; set; }
    [DynamoDBRangeKey] public int StreamRevision { get; set; }
    [DynamoDBProperty] public required string CommitId { get; set; }
    [DynamoDBProperty] public int CommitSequence { get; set; }
    [DynamoDBProperty] public long CommitStamp { get; set; }
    [DynamoDBProperty] public byte[] Headers { get; set; } = Array.Empty<byte>();
    [DynamoDBProperty] public byte[] Events { get; set; } = Array.Empty<byte>();

    public static DynamoDbCommit FromStream(IEventStream eventStream, Guid? commitId, ISerialize serializer)
    {
        var sequence = eventStream.CommitSequence + 1;
        return new DynamoDbCommit
        {
            BucketAndStreamAndSequence = $"{eventStream.BucketId}{eventStream.StreamId}{sequence}",
            BucketId = eventStream.BucketId,
            StreamId = eventStream.StreamId,
            StreamRevision = eventStream.StreamRevision,
            CommitId = (commitId ?? Guid.NewGuid()).ToString("N"),
            CommitSequence = sequence,
            CommitStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Headers = serializer.Serialize(eventStream.UncommittedHeaders),
            Events = serializer.Serialize(eventStream.UncommittedEvents)
        };
    }
}
