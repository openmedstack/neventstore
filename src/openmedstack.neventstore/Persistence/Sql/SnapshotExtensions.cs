namespace OpenMedStack.NEventStore.Persistence.Sql
{
    using System;
    using System.Data;
    using Microsoft.Extensions.Logging;
    using NEventStore;
    using Serialization;

    internal static class SnapshotExtensions
    {
        private const int BucketIdIndex = 0;
        private const int StreamRevisionIndex = 2;
        private const int PayloadIndex = 3;
        public static Snapshot GetSnapshot(this IDataRecord record, ISerialize serializer, string streamIdOriginal, ILogger? logger = null)
        {
            logger?.LogTrace(PersistenceMessages.DeserializingSnapshot);

            var payload = serializer.Deserialize<object>(record, PayloadIndex);
            return new Snapshot(
                record[BucketIdIndex].ToString()!,
                streamIdOriginal,
                record[StreamRevisionIndex].ToInt(),
                payload ?? throw new Exception("Could not deserialize payload"));
        }
    }
}