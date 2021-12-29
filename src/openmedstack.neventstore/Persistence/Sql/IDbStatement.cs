namespace OpenMedStack.NEventStore.Persistence.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IDbStatement : IDisposable
    {
        int PageSize { get; set; }

        void AddParameter(string name, object value, DbType? parameterType = null);

        Task<int> ExecuteNonQuery(string commandText);

        Task<int> ExecuteWithoutExceptions(string commandText);

        Task<object?> ExecuteScalar(string commandText);

        IAsyncEnumerable<IDataRecord> ExecuteWithQuery(string queryText, CancellationToken cancellationToken);
    }
}