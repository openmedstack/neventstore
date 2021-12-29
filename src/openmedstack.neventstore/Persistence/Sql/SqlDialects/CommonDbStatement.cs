namespace OpenMedStack.NEventStore.Persistence.Sql.SqlDialects
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Sql;

    public class CommonDbStatement : IDbStatement
    {
        private readonly ILogger _logger;
        private readonly IDbConnection _connection;

        public CommonDbStatement(
            ISqlDialect dialect,
            IDbConnection connection,
            ILogger logger)
        {
            Parameters = new Dictionary<string, Tuple<object, DbType?>>();

            Dialect = dialect;
            _connection = connection;
            _logger = logger;
        }

        protected IDictionary<string, Tuple<object, DbType?>> Parameters { get; }

        protected ISqlDialect Dialect { get; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual int PageSize { get; set; }

        public virtual void AddParameter(string name, object value, DbType? parameterType = null)
        {
            _logger.LogDebug(PersistenceMessages.AddingParameter, name);
            Parameters[name] = Tuple.Create(Dialect.CoalesceParameterValue(value), parameterType);
        }

        public virtual Task<int> ExecuteWithoutExceptions(string commandText)
        {
            try
            {
                return ExecuteNonQuery(commandText);
            }
            catch (Exception)
            {
                _logger.LogDebug(PersistenceMessages.ExceptionSuppressed);
                return Task.FromResult(0);
            }
        }

        public virtual Task<int> ExecuteNonQuery(string commandText)
        {
            try
            {
                var totalRowsAffected = 0;
                foreach (var text in commandText.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    using var command = BuildCommand(text);
                    totalRowsAffected += command.ExecuteNonQuery();
                }
                return Task.FromResult(totalRowsAffected);
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

        public virtual Task<object?> ExecuteScalar(string commandText)
        {
            try
            {
                using var command = BuildCommand(commandText);
                var affected = command.ExecuteScalar();
                return Task.FromResult(affected);
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

        public virtual IAsyncEnumerable<IDataRecord> ExecuteWithQuery(
            string queryText,
            CancellationToken cancellationToken)
        {
            return ExecuteQuery(queryText, cancellationToken);
        }

        protected virtual void Dispose(bool disposing)
        {
            _logger.LogTrace(PersistenceMessages.DisposingStatement);
            _connection?.Close();
            _connection?.Dispose();
        }

        protected virtual async IAsyncEnumerable<IDataRecord> ExecuteQuery(string queryText, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Parameters.Add(Dialect.Skip, Tuple.Create((object)0, (DbType?)null));
            var command = BuildCommand(queryText);
            using var reader = command is DbCommand dbcmd
                   ? await dbcmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false)
                   : command.ExecuteReader();
            while (reader.Read())
            {
                yield return reader;
            }
            //try
            //{
            //    return new PagedEnumerationCollection(Dialect, command, nextpage, pageSize, cancellationToken, this);
            //}
            //catch (Exception)
            //{
            //    command.Dispose();
            //    throw;
            //}
        }

        protected virtual IDbCommand BuildCommand(string statement)
        {
            _logger.LogTrace(PersistenceMessages.CreatingCommand);
            var command = _connection.CreateCommand();

            if (Settings.CommandTimeout > 0)
            {
                command.CommandTimeout = Settings.CommandTimeout;
            }

            command.CommandText = statement;

            _logger.LogTrace(PersistenceMessages.CommandTextToExecute, statement);

            BuildParameters(command);

            return command;
        }

        protected virtual void BuildParameters(IDbCommand command)
        {
            foreach (var (key, (item, dbType)) in Parameters)
            {
                BuildParameter(command, key, item, dbType);
            }
        }

        protected virtual void BuildParameter(IDbCommand command, string name, object value, DbType? dbType)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            SetParameterValue(parameter, value, dbType);

            _logger.LogTrace(PersistenceMessages.BindingParameter, name, parameter.Value);
            command.Parameters.Add(parameter);
        }

        protected virtual void SetParameterValue(IDataParameter param, object value, DbType? type)
        {
            param.DbType = type ?? (value == null ? DbType.Binary : param.DbType);
            param.Value = value ?? DBNull.Value;
        }
    }
}