namespace OpenMedStack.NEventStore.Persistence.Sql.SqlDialects;

using System;
using System.Data;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Sql;

public class OracleNativeDialect : CommonSqlDialect
{
    private Action<IConnectionFactory, IDbConnection, IDbStatement, byte[]>? _addPayloadParamater;

    /// <inheritdoc />
    public OracleNativeDialect(ILogger logger)
        : base(logger)
    {
    }

    public override string AppendSnapshotToCommit => OracleNativeStatements.AppendSnapshotToCommit;

    public override string CheckpointNumber => MakeOracleParameter(base.CheckpointNumber);

    public override string CommitId => MakeOracleParameter(base.CommitId);

    public override string CommitSequence => MakeOracleParameter(base.CommitSequence);

    public override string CommitStamp => MakeOracleParameter(base.CommitStamp);

    public override string CommitStampEnd => MakeOracleParameter(base.CommitStampEnd);

    public override string CommitStampStart => MakeOracleParameter(base.CommitStampStart);

    public override string DuplicateCommit => OracleNativeStatements.DuplicateCommit;

    public override string GetSnapshot => OracleNativeStatements.GetSnapshot;

    public override string GetCommitsFromStartingRevision =>
        LimitedQuery(OracleNativeStatements.GetCommitsFromStartingRevision);

    public override string GetCommitsFromInstant => OracleNativeStatements.GetCommitsFromInstant;

    public override string GetCommitsFromCheckpoint => OracleNativeStatements.GetCommitsSinceCheckpoint;

    public override string GetCommitsFromBucketAndCheckpoint =>
        OracleNativeStatements.GetCommitsFromBucketAndCheckpoint;

    public override string GetStreamsRequiringSnapshots =>
        LimitedQuery(OracleNativeStatements.GetStreamsRequiringSnapshots);

    public override string InitializeStorage => OracleNativeStatements.InitializeStorage;

    public override string Limit => MakeOracleParameter(base.Limit);

    public override string PersistCommit => OracleNativeStatements.PersistCommit;

    public override string PurgeStorage => OracleNativeStatements.PurgeStorage;

    public override string DeleteStream => OracleNativeStatements.DeleteStream;

    public override string Drop => OracleNativeStatements.DropTables;

    public override string Skip => MakeOracleParameter(base.Skip);

    public override string BucketId => MakeOracleParameter(base.BucketId);

    public override string StreamId => MakeOracleParameter(base.StreamId);

    public override string StreamIdOriginal => MakeOracleParameter(base.StreamIdOriginal);

    public override string Threshold => MakeOracleParameter(base.Threshold);

    public override string Payload => MakeOracleParameter(base.Payload);

    public override string StreamRevision => MakeOracleParameter(base.StreamRevision);

    public override string MaxStreamRevision => MakeOracleParameter(base.MaxStreamRevision);

    public override IDbStatement BuildStatement(IDbConnection connection) =>
        new OracleDbStatement(this, connection, Logger);

    public override object CoalesceParameterValue(object value)
    {
        if (value is Guid guid)
        {
            value = guid.ToByteArray();
        }

        return value;
    }

    private static string ExtractOrderBy(ref string query)
    {
        var orderByIndex = query.IndexOf("ORDER BY", StringComparison.Ordinal);
        var result = query[orderByIndex..].Replace(";", string.Empty);

        return result;
    }

    public override bool IsDuplicate(Exception exception) => exception.Message.Contains("ORA-00001");

    public override NextPageDelegate NextPageDelegate
    {
        get { return (q, r) => { }; }
    }

    public override void AddPayloadParamater(
        IConnectionFactory connectionFactory,
        IDbConnection connection,
        IDbStatement cmd,
        byte[] payload)
    {
        if (_addPayloadParamater == null)
        {
            var dbProviderAssemblyName = connectionFactory.GetDbProviderFactoryType().Assembly.GetName().Name;
            const string oracleManagedDataAcccessAssemblyName = "Oracle.ManagedDataAccess";
            const string oracleDataAcccessAssemblyName = "Oracle.DataAccess";
            if (dbProviderAssemblyName!.Equals(oracleManagedDataAcccessAssemblyName, StringComparison.Ordinal))
            {
                _addPayloadParamater = CreateOraAddPayloadAction(oracleManagedDataAcccessAssemblyName);
            }
            else if (dbProviderAssemblyName.Equals(oracleDataAcccessAssemblyName, StringComparison.Ordinal))
            {
                _addPayloadParamater = CreateOraAddPayloadAction(oracleDataAcccessAssemblyName);
            }
            else
            {
                _addPayloadParamater = (connectionFactory2, connection2, cmd2, payload2) =>
                    base.AddPayloadParamater(connectionFactory2, connection2, cmd2, payload2);
            }
        }

        _addPayloadParamater(connectionFactory, connection, cmd, payload);
    }

    private Action<IConnectionFactory, IDbConnection, IDbStatement, byte[]> CreateOraAddPayloadAction(
        string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);
        var oracleParamaterType = assembly.GetType(assemblyName + ".Client.OracleParameter", true);
        var oracleParamaterValueProperty = oracleParamaterType!.GetProperty("Value");
        var oracleBlobType = assembly.GetType(assemblyName + ".Types.OracleBlob", true);
        var oracleBlobWriteMethod = oracleBlobType!.GetMethod(
            "Write",
            new[] { typeof(byte[]), typeof(int), typeof(int) });
        var oracleParamapterType = assembly.GetType(assemblyName + ".Client.OracleDbType", true);
        var blobField = oracleParamapterType!.GetField("Blob");
        var blobDbType = blobField!.GetValue(null);

        return (_, connection2, cmd2, payload2) =>
        {
            var payloadParam = Activator.CreateInstance(oracleParamaterType, new[] { Payload, blobDbType });
            ((OracleDbStatement)cmd2).AddParameter(Payload, payloadParam!);
            object oracleConnection = ((ConnectionScope)connection2).Current;
            var oracleBlob = Activator.CreateInstance(oracleBlobType, new[] { oracleConnection });
            oracleBlobWriteMethod!.Invoke(oracleBlob, new object[] { payload2, 0, payload2.Length });
            oracleParamaterValueProperty!.SetValue(payloadParam, oracleBlob, null);
        };
    }

    private static string LimitedQuery(string query)
    {
        query = RemovePaging(query);
        if (query.EndsWith(";"))
        {
            query = query.TrimEnd(new[] { ';' });
        }

        var value = string.Format(OracleNativeStatements.LimitedQueryFormat, query);
        return value;
    }

    private static string MakeOracleParameter(string parameterName) => parameterName.Replace('@', ':');

    private static string RemovePaging(string query) =>
        query.Replace("\n LIMIT @Limit OFFSET @Skip;", ";")
            .Replace("\n LIMIT @Limit;", ";")
            .Replace("WHERE ROWNUM <= :Limit;", ";")
            .Replace("\r\nWHERE ROWNUM <= (:Skip + 1) AND ROWNUM  > :Skip", ";");
}