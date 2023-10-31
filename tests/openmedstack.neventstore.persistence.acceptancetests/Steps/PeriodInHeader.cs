using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

public partial class PersistenceEngineBehavior
{
    [Given(@"a persisted stream with a header containing a period")]
    public Task GivenAPersistedStreamWithAHeaderContainingAPeriod()
    {
        _streamId = Guid.NewGuid().ToString();
        var attempt = OptimisticEventStream.Create(
            Bucket.Default,
            _streamId, NullLogger<OptimisticEventStream>.Instance);
        attempt.Add(new EventMessage(new ExtensionMethods.SomeDomainEvent { SomeProperty = "Test" }));
        attempt.Add("key.1", "value");

        return Persistence.Commit(attempt);
    }

    [When(@"getting commit")]
    public async Task WhenGettingCommit()
    {
        _persisted = await Persistence
            .GetFrom(Bucket.Default, _streamId, 0, int.MaxValue, CancellationToken.None).First()
            .ConfigureAwait(false);
    }

    [Then(@"should return the header with the period")]
    public void ThenShouldReturnTheHeaderWithThePeriod()
    {
        Assert.Contains("key.1", _persisted.Headers.Keys);
    }
}
