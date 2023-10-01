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
        CreatePersistence = pageSize => new SqlPersistenceFactory(
            new NetStandardConnectionFactory(
                NpgsqlFactory.Instance,
                "Server=127.0.0.1;Keepalive=1;Pooling=true;MinPoolSize=1;MaxPoolSize=20;Port=5432;Database=eventdb;User Id=openmedstack;Password=openmedstack;",
                NullLogger<NetStandardConnectionFactory>.Instance),
            new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance),
            new PostgreSqlDialect(NullLogger.Instance),
            NullLogger.Instance,
            pageSize: pageSize).Build();
    }
}
