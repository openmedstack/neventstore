namespace OpenMedStack.NEventStore.Persistence.Sql.SqlDialects
{
    using System;
    using Microsoft.Extensions.Logging;

    public class PostgreSqlDialect : CommonSqlDialect
    {
        /// <inheritdoc />
        public PostgreSqlDialect(ILogger logger)
            : base(logger)
        {
        }

        public override string InitializeStorage => PostgreSqlStatements.InitializeStorage;

        public override string PersistCommit => PostgreSqlStatements.PersistCommits;

        public override bool IsDuplicate(Exception exception)
        {
            var message = exception.Message.ToUpperInvariant();
            return message.Contains("23505") || message.Contains("IX_COMMITS_COMMITSEQUENCE");
        }
    }
}
