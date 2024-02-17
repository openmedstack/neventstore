using Amazon.DynamoDBv2.DataModel;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.DynamoDb;

[DynamoDBTable("snapshots", LowerCamelCaseProperties = false)]
internal class DynamoDbSnapshots
{
    [DynamoDBHashKey] public required string BucketAndStreamAndRevision { get; set; }
    [DynamoDBProperty] public required string BucketId { get; set; }
    [DynamoDBProperty] public required string StreamId { get; set; }
    [DynamoDBProperty] public int StreamRevision { get; set; }
    [DynamoDBProperty] public byte[] Payload { get; set; } = Array.Empty<byte>();

    public static DynamoDbSnapshots FromSnapshot(ISnapshot snapshot, ISerialize serializer)
    {
        return new DynamoDbSnapshots
        {
            BucketAndStreamAndRevision = $"{snapshot.BucketId}{snapshot.StreamId}",
            BucketId = snapshot.BucketId,
            StreamId = snapshot.StreamId,
            StreamRevision = snapshot.StreamRevision,
            Payload = serializer.Serialize(snapshot.Payload)
        };
    }
}
