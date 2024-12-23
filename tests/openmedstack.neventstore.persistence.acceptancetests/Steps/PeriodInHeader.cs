using OpenMedStack.NEventStore.Abstractions;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

public partial class PersistenceEngineBehavior
{
    private const string TenantId = "default";

    [Given(@"a persisted stream with a header containing a period")]
    public Task GivenAPersistedStreamWithAHeaderContainingAPeriod()
    {
        _streamId = Guid.NewGuid().ToString();
        var attempt = new CommitAttempt(
            TenantId,
            _streamId,
            1,
            Guid.NewGuid(),
            1,
            DateTimeOffset.UtcNow,
            new Dictionary<string, object>
            {
                ["key.1"] = "value"
            },
            [new EventMessage(new ExtensionMethods.SomeDomainEvent { SomeProperty = "Test" })]);

        return Persistence.Commit(attempt);
    }

    [When(@"getting commit")]
    public async Task WhenGettingCommit()
    {
        _persisted = await Persistence
            .Get(TenantId, _streamId).First()
            .ConfigureAwait(false);
    }

    [Then(@"should return the header with the period")]
    public void ThenShouldReturnTheHeaderWithThePeriod()
    {
        Assert.Contains("key.1", _persisted.Headers.Keys);
    }
}
