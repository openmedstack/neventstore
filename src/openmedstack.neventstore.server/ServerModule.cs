using Autofac;

namespace OpenMedStack.NEventStore.Server;

internal class ServerModule : Module
{
    private readonly EventStoreConfiguration _configuration;

    public ServerModule(EventStoreConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterInstance(new ConfigurationTenantProvider(_configuration));
    }
}
