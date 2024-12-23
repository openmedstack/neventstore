using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.S3;

internal class S3Snapshot
{
    public required string TenantId { get; set; }
    public required string StreamId { get; set; }
    public int StreamRevision { get; set; }
    public byte[] Payload { get; set; } = [];

    public static S3Snapshot FromSnapshot(ISnapshot snapshot, ISerialize serializer)
    {
        return new S3Snapshot
        {
            TenantId = snapshot.TenantId,
            StreamId = snapshot.StreamId,
            StreamRevision = snapshot.StreamRevision,
            Payload = serializer.Serialize(snapshot.Payload)
        };
    }

    public ISnapshot ToSnapshot(ISerialize serializer)
    {
        return new Snapshot(TenantId, StreamId, StreamRevision, serializer.Deserialize<object>(Payload)!);
    }
}
