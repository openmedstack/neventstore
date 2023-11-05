using Grpc.Net.Client;
using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.GrpcClient;
using OpenMedStack.NEventStore.HttpClient;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Server.Tests.Steps;

public partial class FeatureSteps
{
    private ICommitEvents _grpcClient = null!;
    private ICommitEvents _httpClient = null!;

    [Given(@"both an GRPC and HTTP client")]
    public void GivenBothAnGrpcAndHttpClient()
    {
        var serializer = new TestJsonSerializer();
        var httpClient = _server.CreateClient();
        _grpcClient = new GrpcEventStorePersistence(new Uri("http://localhost"), serializer,
            new GrpcChannelOptions { HttpClient = httpClient });
        _httpClient = new HttpEventStorePersistence(httpClient, serializer);
    }

    [When(@"I commit an event to the event store using the (.+) client")]
    public async Task WhenICommitAnEventToTheEventStoreUsingTheHttpClient(string type)
    {
        var commit = OptimisticEventStream.Create("test", Guid.NewGuid().ToString("N"),
            NullLogger<OptimisticEventStream>.Instance);
        commit.Add(new EventMessage(new TestEvent("test", "test_case", 1, DateTimeOffset.UtcNow)));
        var client = GetClient(type);
        _commitResult = await client.Commit(commit).ConfigureAwait(false);
    }

    [Then(@"I can load the event stream from the event store using the (.+) client")]
    public async Task ThenICanLoadTheEventStreamFromTheEventStoreUsingTheGrpcClient(string type)
    {
        var client = GetClient(type);
        var stream = client.Get("test", _commitResult!.StreamId, 0, int.MaxValue, CancellationToken.None);
        _streamCount = await stream.Count().ConfigureAwait(false);
    }

    private ICommitEvents GetClient(string type) => type switch
    {
        "HTTP" => _httpClient,
        "GRPC" => _grpcClient,
        _ => throw new ArgumentException($@"Unknown type {type}", nameof(type))
    };
}
