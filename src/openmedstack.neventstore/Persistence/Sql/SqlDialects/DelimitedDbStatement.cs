namespace OpenMedStack.NEventStore.Persistence.Sql.SqlDialects;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sql;

public class DelimitedDbStatement : CommonDbStatement
{
    private const string Delimiter = ";";

    public DelimitedDbStatement(ISqlDialect dialect, IDbConnection connection, ILogger logger)
        : base(dialect, connection, logger)
    {
    }

    public override async Task<int> ExecuteNonQuery(string commandText)
    {
        var result = 0;
        foreach (var s in SplitCommandText(commandText))
        {
            result += await base.ExecuteNonQuery(s).ConfigureAwait(false);
        }

        return result;
    }

    private static IEnumerable<string> SplitCommandText(string delimited)
    {
        if (string.IsNullOrEmpty(delimited))
        {
            return Array.Empty<string>();
        }

        return delimited.Split(Delimiter.ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
            .AsEnumerable()
            .Select(x => x + Delimiter)
            .ToArray();
    }
}