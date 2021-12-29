// // --------------------------------------------------------------------------------------------------------------------
// // <copyright file="FirebirdSqlStatement.cs" company="Reimers.dk">
// //
// // </copyright>
// // <summary>
// //   Defines the FirebirdSqlStatement type.
// // </summary>
// // --------------------------------------------------------------------------------------------------------------------

namespace OpenMedStack.NEventStore.Persistence.Sql.SqlDialects
{
    using System;
    using System.Data;
    using Microsoft.Extensions.Logging;

    internal class FirebirdSqlStatement : CommonDbStatement
    {
        /// <inheritdoc />
        public FirebirdSqlStatement(ISqlDialect dialect, IDbConnection connection, ILogger logger)
            : base(dialect, connection, logger)
        {
        }

        /// <inheritdoc />
        protected override void BuildParameter(IDbCommand command, string name, object value, DbType? dbType)
        {
            switch (dbType)
            {
                case DbType.DateTimeOffset:
                    base.BuildParameter(command, name, ((DateTimeOffset)value).DateTime, DbType.DateTime);
                    break;
                default:
                    base.BuildParameter(command, name, value, dbType);
                    break;
            }
        }
    }
}
