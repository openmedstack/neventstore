// ReSharper disable once CheckNamespace
namespace NEventStore
{
    using System.Data.Common;
    using OpenMedStack.NEventStore;
    using OpenMedStack.NEventStore.Persistence.Sql;

    public static class SqlPersistenceWireupExtensions
    {
        public static SqlPersistenceWireup UsingSqlPersistence(
            this Wireup wireup,
            DbProviderFactory providerFactory,
            string connectionString)
        {
            var factory = new NetStandardConnectionFactory(providerFactory, connectionString, wireup.Logger);
            return wireup.UsingSqlPersistence(factory);
        }

        public static SqlPersistenceWireup UsingSqlPersistence(this Wireup wireup, IConnectionFactory factory) =>
            new(wireup, factory);

        public static SqlPersistenceWireup WithCommandTimeout(this SqlPersistenceWireup wireup, int commandTimeout)
        {
            Settings.CommandTimeout = commandTimeout;
            return wireup;
        }
    }
}
