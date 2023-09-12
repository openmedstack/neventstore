using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.Persistence.Sql;

using System;
using System.Collections.Generic;
using System.Data;

public static class CommitExtensions
{
    private const int BucketIdIndex = 0;
    //private const int StreamIdIndex = 1;
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

        var commit = new Commit(record.GetString(BucketIdIndex),
            record.GetString(StreamIdOriginalIndex),
            record.GetInt32(StreamRevisionIndex),
            record.GetGuid(CommitIdIndex),
            record.GetInt32(CommitSequenceIndex),
            sqlDialect.ToDateTime(record[CommitStampIndex]),
            record.GetInt64(CheckpointIndex),
            headers!,
            events!);

        return commit;
    }

    public static int CommitSequence(this IDataRecord record) => record[CommitSequenceIndex].ToInt();

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
