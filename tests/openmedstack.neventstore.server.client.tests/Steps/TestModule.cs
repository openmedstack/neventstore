using Autofac;
using Microsoft.Extensions.Logging;
using OpenMedStack.NEventStore.Abstractions;
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
        builder.Register(ctx =>
                Wireup.Init(ctx.Resolve<ILoggerFactory>()).UsingInMemoryPersistence().UsingJsonSerialization().Build())
            .As<IStoreEvents>().AutoActivate().SingleInstance();
        builder.Register(ctx => ctx.Resolve<IStoreEvents>().Advanced).As<IPersistStreams>().SingleInstance();
        builder.RegisterInstance(new ConfigurationTenantProvider(_configuration));
    }
}
