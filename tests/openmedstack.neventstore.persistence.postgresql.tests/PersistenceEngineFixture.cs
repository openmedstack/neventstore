namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests;

using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Persistence.Sql;
using OpenMedStack.NEventStore.Persistence.Sql.SqlDialects;
using OpenMedStack.NEventStore.Serialization;
using Npgsql;
using OpenMedStack.NEventStore.Persistence.AcceptanceTests;

public class PersistenceEngineFixture : PersistenceEngineFixtureBase
{
    public PersistenceEngineFixture()
    {
        CreatePersistence = pageSize =>
        {
            var engine = new SqlPersistenceEngine(
                new NetStandardConnectionFactory(
                    NpgsqlFactory.Instance,
                    "Server=127.0.0.1;Keepalive=1;Pooling=true;MinPoolSize=1;MaxPoolSize=20;Port=5432;Database=eventdb;User Id=openmedstack;Password=openmedstack;",
                    NullLogger<NetStandardConnectionFactory>.Instance),
                new PostgreSqlDialect(NullLogger<PostgreSqlDialect>.Instance),
                new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance),
                pageSize,
                new Sha1StreamIdHasher(),
                NullLogger<SqlPersistenceEngine>.Instance);
            return (engine, engine, engine);
        };
    }
}
