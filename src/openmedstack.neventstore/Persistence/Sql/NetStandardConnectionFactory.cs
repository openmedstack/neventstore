using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Persistence.Sql;

using System;
using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;

public class NetStandardConnectionFactory : IConnectionFactory
{
    private readonly ILogger<NetStandardConnectionFactory> _logger;
    private readonly DbProviderFactory _providerFactory;
    private readonly string _connectionString;

    public NetStandardConnectionFactory(
        DbProviderFactory providerFactory,
        string connectionString,
        ILogger<NetStandardConnectionFactory> logger)
    {
        _providerFactory = providerFactory;
        _connectionString = connectionString;
        _logger = logger;
    }

    public Type GetDbProviderFactoryType() => _providerFactory.GetType();

    public IDbConnection Open()
    {
        _logger.LogTrace(PersistenceMessages.OpeningMasterConnection, _connectionString);
        return Open(_connectionString);
    }

    protected virtual IDbConnection Open(string connectionString)
    {
        return OpenConnection(connectionString);
    }

    protected virtual IDbConnection OpenConnection(string connectionString)
    {
        var factory = _providerFactory;
        var connection = factory.CreateConnection();
        if (connection == null)
        {
            throw new ConfigurationErrorsException(PersistenceMessages.BadConnectionName);
        }

        connection.ConnectionString = connectionString;
        try
        {
            _logger.LogTrace(PersistenceMessages.OpeningConnection, connectionString);
            connection.Open();
        }
        catch (Exception e)
        {
            _logger.LogWarning(PersistenceMessages.OpenFailed, connectionString);
            throw new StorageUnavailableException(e.Message, e);
        }

        return connection;
    }
}
