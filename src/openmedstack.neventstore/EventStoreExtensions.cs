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
        where TDialect : ISqlDialect
        where THasher : IStreamIdHasher
    {
        serviceCollection.AddSingleton<IStreamIdHasher, Sha1StreamIdHasher>();
        serviceCollection.AddSingleton<SqlPersistenceEngine>(sp => new SqlPersistenceEngine(
            new NetStandardConnectionFactory(dbProviderFactory, connectionString,
                sp.GetRequiredService<ILogger<NetStandardConnectionFactory>>()),
            sp.GetRequiredService<TDialect>(),
            sp.GetRequiredService<ISerialize>(),
            defaultPageSize, sp.GetRequiredService<THasher>(),
            sp.GetRequiredService<ILogger<SqlPersistenceEngine>>()));
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
