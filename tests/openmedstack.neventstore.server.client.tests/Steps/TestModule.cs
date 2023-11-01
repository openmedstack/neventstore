using Autofac;
using OpenMedStack.Commands;
using OpenMedStack.Events;
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
        builder.RegisterType<NullRouter>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<NullPublisher>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<TestJsonSerializer>().AsImplementedInterfaces();
        builder.RegisterType<NesJsonSerializer>().As<ISerialize>().SingleInstance();
        builder.RegisterType<InMemoryPersistenceEngine>().AsImplementedInterfaces().SingleInstance().AutoActivate()
            .OnActivated(x => x.Instance.InitializeStorageEngine());
        builder.RegisterInstance(new ConfigurationTenantProvider(_configuration));
    }
}

internal class NullPublisher : IPublishEvents
{
    public Task Publish<T>(
        T message,
        IDictionary<string, object>? headers = null,
        CancellationToken cancellationToken = new CancellationToken()) where T : BaseEvent
    {
        return Task.CompletedTask;
    }
}

internal class NullRouter : IRouteCommands
{
    public Task<CommandResponse> Send<T>(
        T command,
        IDictionary<string, object>? headers = null,
        CancellationToken cancellationToken = new CancellationToken()) where T : DomainCommand
    {
        return Task.FromResult(command.CreateResponse());
    }
}
