using System.Diagnostics.CodeAnalysis;

namespace OpenMedStack.NEventStore.Persistence.Sql.SqlDialects;

using System;
using Microsoft.Extensions.Logging;

public class MySqlDialect : CommonSqlDialect
{
    private const int UniqueKeyViolation = 1062;

    /// <inheritdoc />
    public MySqlDialect(ILogger<MySqlDialect> logger)
        : base(logger)
    {
    }

    public override string InitializeStorage => MySqlStatements.InitializeStorage;

    public override string PersistCommit => MySqlStatements.PersistCommit;

    public override string AppendSnapshotToCommit =>
        base.AppendSnapshotToCommit.Replace("/*FROM DUAL*/", "FROM DUAL");

    public override object CoalesceParameterValue(object value)
    {
        if (value is Guid guid)
        {
            return guid.ToByteArray();
        }

        if (value is DateTime time)
        {
            return time.Ticks;
        }

        return value;
    }

    [UnconditionalSuppressMessage("Missing type annotation.", "IL2075")]
    public override bool IsDuplicate(Exception exception)
    {
        var type = exception.GetType();
        var property = type.GetProperty("Number")!;
        return UniqueKeyViolation == (int)property.GetValue(exception, null)!;
    }
}
