namespace OpenMedStack.NEventStore.Persistence.Sql
{
    using System;
    using System.Data;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Extensions.Logging;

    public class ConnectionScope : ThreadScope<IDbConnection>, IDbConnection
    {
        public ConnectionScope(string connectionName, Func<IDbConnection> factory, ILogger logger)
            : base(connectionName, factory, logger)
        { }

        IDbTransaction IDbConnection.BeginTransaction() => Current.BeginTransaction();

        IDbTransaction IDbConnection.BeginTransaction(IsolationLevel il) => Current.BeginTransaction(il);

        void IDbConnection.Close()
        {
            // no-op--let Dispose do the real work.
        }

        void IDbConnection.ChangeDatabase(string databaseName)
        {
            Current.ChangeDatabase(databaseName);
        }

        IDbCommand IDbConnection.CreateCommand() => Current.CreateCommand();

        void IDbConnection.Open()
        {
            Current.Open();
        }

        [AllowNull]
        string IDbConnection.ConnectionString
        {
            get => Current.ConnectionString;
            set => Current.ConnectionString = value;
        }
        
        int IDbConnection.ConnectionTimeout => Current.ConnectionTimeout;

        string IDbConnection.Database => Current.Database;

        ConnectionState IDbConnection.State => Current.State;

        protected override void Dispose(bool disposing)
        {
            if (Current.State != ConnectionState.Closed && Current?.State != ConnectionState.Broken)
            {
                Current?.Close();
            }
            base.Dispose(disposing);
        }
    }
}