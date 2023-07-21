namespace OpenMedStack.NEventStore.Persistence.Sql.SqlDialects;

using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sql;

public class OracleDbStatement : CommonDbStatement
{
    private readonly ISqlDialect _dialect;

    public OracleDbStatement(ISqlDialect dialect, IDbConnection connection, ILogger logger)
        : base(dialect, connection, logger)
    {
        _dialect = dialect;
    }

    public override void AddParameter(string name, object value, DbType? dbType = null)
    {
        name = name.Replace('@', ':');

        if (value is Guid guid)
        {
            base.AddParameter(name, guid.ToByteArray(), null);
        }
        else
        {
            base.AddParameter(name, value, dbType);
        }
    }

    public override async Task<int> ExecuteNonQuery(string commandText)
    {
        try
        {
            using var command = BuildCommand(commandText);
            if (command is DbCommand dbcmd)
            {
                return await dbcmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            return command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            if (Dialect.IsDuplicate(e))
            {
                throw new UniqueKeyViolationException(e.Message, e);
            }

            throw;
        }
    }

    protected override IDbCommand BuildCommand(string statement)
    {
        var command = base.BuildCommand(statement);
        var pi = command.GetType().GetProperty("BindByName");
        if (pi != null)
        {
            pi.SetValue(command, true, null);
        }

        return command;
    }

    protected override void BuildParameter(IDbCommand command, string name, object value, DbType? dbType)
    {
        //HACK
        if (name == _dialect.Payload && value is DbParameter)
        {
            command.Parameters.Add(value);
            return;
        }

        base.BuildParameter(command, name, value, dbType);
    }
}