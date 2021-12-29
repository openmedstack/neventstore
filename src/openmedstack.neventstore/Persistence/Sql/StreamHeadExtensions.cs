namespace OpenMedStack.NEventStore.Persistence.Sql
{
    using System.Data;
    using Persistence;

    internal static class StreamHeadExtensions
    {
        private const int BucketIdIndex = 0;
        private const int StreamIdIndex = 2;
        private const int HeadRevisionIndex = 3;
        private const int SnapshotRevisionIndex = 4;

        public static StreamHead GetStreamToSnapshot(this IDataRecord record) =>
            new(
                record[BucketIdIndex].ToString()!,
                record[StreamIdIndex].ToString()!,
                record[HeadRevisionIndex].ToInt(),
                record[SnapshotRevisionIndex].ToInt());
    }
}