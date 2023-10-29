using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Persistence.Sql;
using OpenMedStack.NEventStore.Serialization;

namespace OpenMedStack.NEventStore;

using OpenMedStack.NEventStore.Persistence.InMemory;

public static class EventStoreExtensions
{
    public static IServiceCollection RegisterInMemoryEventStore(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryPersistenceEngine>();
        services.AddTransient<IManagePersistence>(sp => sp.GetRequiredService<InMemoryPersistenceEngine>());
        services.AddTransient<ICommitEvents>(sp => sp.GetRequiredService<InMemoryPersistenceEngine>());
        services.AddTransient<IAccessSnapshots>(sp => sp.GetRequiredService<InMemoryPersistenceEngine>());
        return services;
    }

    public static IServiceCollection RegisterSqlEventStore<TDialect, THasher>(
        this IServiceCollection serviceCollection,
        DbProviderFactory dbProviderFactory,
        string connectionString,
        int defaultPageSize = 128)
        where TDialect : class, ISqlDialect
        where THasher : class, IStreamIdHasher
    {
        serviceCollection.AddSingleton<ISqlDialect, TDialect>();
        serviceCollection.AddSingleton<IStreamIdHasher, THasher>();
        serviceCollection.AddSingleton<SqlPersistenceEngine>(sp => new SqlPersistenceEngine(
            new NetStandardConnectionFactory(dbProviderFactory, connectionString,
                sp.GetRequiredService<ILogger<NetStandardConnectionFactory>>()),
            sp.GetRequiredService<ISqlDialect>(),
            sp.GetRequiredService<ISerialize>(),
            defaultPageSize, sp.GetRequiredService<IStreamIdHasher>(),
            sp.GetRequiredService<ILogger<SqlPersistenceEngine>>()));
        serviceCollection.AddSingleton<ICommitEvents>(sp=>sp.GetRequiredService<SqlPersistenceEngine>());
        serviceCollection.AddSingleton<IAccessSnapshots>(sp=>sp.GetRequiredService<SqlPersistenceEngine>());
        serviceCollection.AddSingleton<IManagePersistence>(sp=>sp.GetRequiredService<SqlPersistenceEngine>());
        return serviceCollection;
    }

    public static IServiceCollection RegisterJsonSerialization(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<ISerialize>(sp =>
            new NesJsonSerializer(sp.GetRequiredService<ILogger<NesJsonSerializer>>()));
        return serviceCollection;
    }

    public static Task InitializeStorageEngine(this IManagePersistence persistence)
    {
        return persistence.Initialize();
    }
}
