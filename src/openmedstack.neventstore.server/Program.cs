using OpenMedStack.Autofac;
using OpenMedStack.Autofac.MassTransit;
using OpenMedStack.Autofac.NEventstore;
using OpenMedStack.Autofac.NEventstore.Sql;
using OpenMedStack.NEventStore.Server.Service;

namespace OpenMedStack.NEventStore.Server;

using Npgsql;
using OpenMedStack;
using OpenMedStack.Web.Autofac;
using OpenMedStack.NEventStore.Persistence.Sql.SqlDialects;

internal class Program
{
    public static async Task Main()
    {
        var configuration = new EventStoreConfiguration
        {
            Name = typeof(EventStoreController).Assembly.GetName().Name!,
            Urls = new[] { "https://localhost:5001" },
            TenantPrefix = "test",
            QueueName = "eventstore",
            ServiceBus = new Uri("loopback://localhost")
        };

        var chassis = Chassis.From(configuration)
            .UsingNEventStore()
            .UsingSqlEventStore<EventStoreConfiguration, PostgreSqlDialect>(NpgsqlFactory.Instance)
            .AddAutofacModules((c, _) => new ServerModule(c))
            //.UsingMassTransitOverRabbitMq()
            .UsingInMemoryMassTransit()
            .UsingWebServer(_ => new DelegateWebApplicationConfiguration(
                collection =>
                {
                    collection.AddResponseCompression();
                    collection.AddGrpc();
                    collection.AddAuthentication();
                    collection.AddAuthorization();
                    collection.AddControllers();
                },
                app =>
                {
                    app.UseHsts();
                    app.UseResponseCompression();
                    app.UseCors();
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(e =>
                    {
                        e.MapGrpcService<EventStoreService>();
                        e.MapControllers();
                    });
                }));
        chassis.Start();
        await chassis.DisposeAsync().ConfigureAwait(false);
    }
}
