using Autofac;
using Microsoft.Extensions.Logging;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Persistence.InMemory;
using OpenMedStack.NEventStore.Serialization;

namespace OpenMedStack.NEventStore.Server.Tests.Steps;

internal class TestModule : Module
{
    private readonly EventStoreConfiguration _configuration;

    public TestModule(EventStoreConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<TestJsonSerializer>().AsImplementedInterfaces();
        builder.RegisterType<NesJsonSerializer>().As<ISerialize>().SingleInstance();
        builder.RegisterType<InMemoryPersistenceEngine>().AsImplementedInterfaces().SingleInstance().AutoActivate()
            .OnActivated(x => x.Instance.InitializeStorageEngine());
        builder.RegisterInstance(new ConfigurationTenantProvider(_configuration));
    }
}
