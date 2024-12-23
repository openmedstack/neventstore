using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.Persistence.Sql;

using System.Data;

internal static class StreamHeadExtensions
{
    private const int TenantIdIndex = 0;
    private const int StreamIdIndex = 2;
    private const int HeadRevisionIndex = 3;
    private const int SnapshotRevisionIndex = 4;

    public static StreamHead GetStreamToSnapshot(this IDataRecord record) =>
        new(
            record[TenantIdIndex].ToString()!,
            record[StreamIdIndex].ToString()!,
            record[HeadRevisionIndex].ToInt(),
            record[SnapshotRevisionIndex].ToInt());
}
