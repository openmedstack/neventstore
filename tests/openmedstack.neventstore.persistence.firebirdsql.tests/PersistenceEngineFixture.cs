// ReSharper disable once CheckNamespace

namespace OpenMedStack.NEventStore.Persistence.FirebirdSql.Tests;

using System;
using global::FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Persistence.AcceptanceTests;
using OpenMedStack.NEventStore.Persistence.Sql;
using OpenMedStack.NEventStore.Persistence.Sql.SqlDialects;
using OpenMedStack.NEventStore.Serialization;

public class PersistenceEngineFixture : PersistenceEngineFixtureBase
{
    private const string ConnectionString =
        "User=SYSDBA;Password=masterkey;Database={0}.fdb;DataSource=localhost;Port = 3050; Dialect = 3; Charset = NONE; Role =; Connection lifetime = 15; Pooling = true;MinPoolSize = 0; MaxPoolSize = 50; Packet Size = 8192; ServerType = 1; ";

    public PersistenceEngineFixture()
    {
        var connectionString = string.Format(ConnectionString, Guid.NewGuid().ToString("N"));
        FbConnection.CreateDatabase(connectionString, overwrite: true);
        CreatePersistence = pageSize => new SqlPersistenceFactory(
            new NetStandardConnectionFactory(FirebirdClientFactory.Instance, connectionString,
                NullLogger<NetStandardConnectionFactory>.Instance),
            new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance),
            new FirebirdSqlDialect(NullLogger.Instance),
            logger: NullLogger.Instance,
            pageSize: pageSize).Build();
    }
}
