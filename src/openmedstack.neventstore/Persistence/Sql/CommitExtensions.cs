using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Persistence.Sql;

using System;
using System.Collections.Generic;
using System.Data;
using Persistence;

public static class CommitExtensions
{
    private const int BucketIdIndex = 0;
    private const int StreamIdIndex = 1;
    private const int StreamIdOriginalIndex = 2;
    private const int StreamRevisionIndex = 3;
    private const int CommitIdIndex = 4;
    private const int CommitSequenceIndex = 5;
    private const int CommitStampIndex = 6;
    private const int CheckpointIndex = 7;
    private const int HeadersIndex = 8;
    private const int PayloadIndex = 9;

    public static ICommit GetCommit(this IDataRecord record, ISerialize serializer, ISqlDialect sqlDialect)
    {
        var headers = serializer.Deserialize<Dictionary<string, object>>(record, HeadersIndex);
        var events = serializer.Deserialize<List<EventMessage>>(record, PayloadIndex);

        var commit = new Commit(record[BucketIdIndex].ToString()!,
            record[StreamIdOriginalIndex].ToString()!,
            record[StreamRevisionIndex].ToInt(),
            record[CommitIdIndex].ToGuid(),
            record[CommitSequenceIndex].ToInt(),
            sqlDialect.ToDateTime(record[CommitStampIndex]),
            record[CheckpointIndex].ToLong(),
            headers!,
            events!);

        return commit;
    }

    public static string StreamId(this IDataRecord record) => record[StreamIdIndex].ToString()!;

    public static int CommitSequence(this IDataRecord record) => record[CommitSequenceIndex].ToInt();

    public static long CheckpointNumber(this IDataRecord record) => record[CheckpointIndex].ToLong();

    public static T? Deserialize<T>(this ISerialize serializer, IDataRecord record, int index)
    {
        if (index >= record.FieldCount)
        {
            return default;
        }

        var value = record[index];
        if (value == DBNull.Value)
        {
            return default;
        }

        var bytes = (byte[])value;
        return bytes.Length == 0 ? default : serializer.Deserialize<T>(bytes);
    }
}