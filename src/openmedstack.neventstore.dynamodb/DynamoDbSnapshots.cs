using Amazon.DynamoDBv2.DataModel;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.DynamoDb;

[DynamoDBTable("snapshots", LowerCamelCaseProperties = false)]
internal class DynamoDbSnapshots
{
    [DynamoDBHashKey] public required string TenantAndStream { get; set; }
    [DynamoDBProperty] public required string TenantId { get; set; }
    [DynamoDBProperty] public required string StreamId { get; set; }
    [DynamoDBRangeKey] public int StreamRevision { get; set; }
    [DynamoDBProperty] public byte[] Payload { get; set; } = [];

    public static DynamoDbSnapshots FromSnapshot(ISnapshot snapshot, ISerialize serializer)
    {
        return new DynamoDbSnapshots
        {
            TenantAndStream = $"{snapshot.TenantId}{snapshot.StreamId}",
            TenantId = snapshot.TenantId,
            StreamId = snapshot.StreamId,
            StreamRevision = snapshot.StreamRevision,
            Payload = serializer.Serialize(snapshot.Payload)
        };
    }
}
