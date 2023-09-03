using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using OpenMedStack.Autofac;
using OpenMedStack.Autofac.NEventstore;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.GrpcClient;
using OpenMedStack.NEventStore.HttpClient;
using OpenMedStack.NEventStore.Server.Service;
using OpenMedStack.Web.Autofac;
using OpenMedStack.Web.Testing;
using TechTalk.SpecFlow;

namespace OpenMedStack.NEventStore.Server.Tests.Steps;

[Binding]
public partial class FeatureSteps : IAsyncDisposable
{
    private TestChassis<EventStoreConfiguration> _server = null!;
    private IPersistStreams _client = null!;

    [Given(@"I have a new event store server")]
    public void GivenIHaveANewEventStoreServer()
    {
        var configuration = new EventStoreConfiguration
            { TenantPrefix = "test", Name = typeof(EventStoreController).Namespace! };
        _server = Chassis.From(configuration)
            .UsingNEventStore()
            //.UsingInMemoryEventStore()
            //.UsingInMemoryEventDispatcher(TimeSpan.FromMilliseconds(100))
            .AddAutofacModules((c, _) => new TestModule(c))
            .UsingTestWebServer(_ => new DelegateWebApplicationConfiguration(
                collection =>
                {
                    collection.AddGrpc();
                    // collection.AddTransient<EventStoreController>();
                    collection.AddControllers().AddNewtonsoftJson(o =>
                    {
                        o.SerializerSettings.Formatting = Formatting.None;
                    });
                },
                app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(e =>
                    {
                        e.MapGrpcService<EventStoreService>();
                        e.MapControllers();
                    });
                }));
        _server.Start();
    }

    [Given(@"an HTTP client")]
    public void GivenAnHttpClient()
    {
        _client = new HttpEventStorePersistence(_server.CreateClient(), new TestJsonSerializer());
    }

    [Given(@"an GRPC client")]
    public void GivenAnGrpcClient()
    {
        _client = new GrpcEventStorePersistence(new Uri("http://localhost"),
            new TestJsonSerializer(), new GrpcChannelOptions{DisposeHttpClient = true, HttpClient = _server.CreateClient()});
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
