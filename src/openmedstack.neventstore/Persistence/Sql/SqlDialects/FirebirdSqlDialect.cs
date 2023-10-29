namespace OpenMedStack.NEventStore.Persistence.Sql.SqlDialects;

using System;
using System.Data;
using Microsoft.Extensions.Logging;

public class FirebirdSqlDialect : CommonSqlDialect
{
    /// <inheritdoc />
    public FirebirdSqlDialect(ILogger<FirebirdSqlDialect> logger)
        : base(logger)
    {
    }

    public override string InitializeStorage => FirebirdSqlStatements.InitializeStorage;

    public override string PersistCommit => FirebirdSqlStatements.PersistCommits;

    public string GetUndispatchedCommits => FirebirdSqlStatements.GetUndispatchedCommits;

    public override string GetCommitsFromInstant => FirebirdSqlStatements.GetCommitsFromInstant;

    public override string GetCommitsFromToInstant => FirebirdSqlStatements.GetCommitsFromToInstant;

    public override string GetCommitsFromStartingRevision => FirebirdSqlStatements.GetCommitsFromStartingRevision;

    public override string GetCommitsFromBucketAndCheckpoint =>
        FirebirdSqlStatements.GetCommitsFromBucketAndCheckpoint;

    public override string GetSnapshot => FirebirdSqlStatements.GetSnapshot;

    public override string GetStreamsRequiringSnapshots => FirebirdSqlStatements.GetStreamsRequiringSnapshots;

    public override string GetCommitsFromCheckpoint => FirebirdSqlStatements.GetCommitsFromCheckpoint;

    public override bool IsDuplicate(Exception exception)
    {
        var message = exception.Message.ToUpperInvariant();
        return message.Contains("DUPLICATE") || message.Contains("UNIQUE") || message.Contains("CONSTRAINT");
    }

    public override string AppendSnapshotToCommit => FirebirdSqlStatements.AppendSnapshotToCommit;

    public override string PurgeStorage => FirebirdSqlStatements.PurgeStorage;

    public override string Drop => FirebirdSqlStatements.DropTables;

    /// <inheritdoc />
    public override IDbStatement BuildStatement(IDbConnection connection) =>
        new FirebirdSqlStatement(this, connection, Logger);
}
