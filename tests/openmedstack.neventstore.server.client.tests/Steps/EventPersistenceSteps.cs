using OpenMedStack.NEventStore.Abstractions;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Server.Tests.Steps;

public partial class FeatureSteps
{
    private ICommit? _commitResult;
    private int _streamCount;

    [When(@"I commit an event to the event store")]
    public async Task WhenICommitAnEventToTheEventStore()
    {
        var commit = new CommitAttempt("test", Guid.NewGuid().ToString("N"),
            1, Guid.NewGuid(), 1, DateTimeOffset.UtcNow, null,
            [new EventMessage(new TestEvent("test", "test_case", 1, DateTimeOffset.UtcNow))]);
        _commitResult = await _client.Commit(commit).ConfigureAwait(false);
    }

    [Then(@"the event is persisted")]
    public void ThenTheEventIsPersisted()
    {
        Assert.NotNull(_commitResult);
    }

    [Then(@"I can load the event stream from the event store")]
    public async Task ThenICanLoadTheEventStreamFromTheEventStore()
    {
        var stream = _client.Get("test", _commitResult!.StreamId, 0, int.MaxValue, CancellationToken.None);
        _streamCount = await stream.Count().ConfigureAwait(false);
    }

    [Then(@"the event stream is returned")]
    public void ThenTheEventStreamIsReturned()
    {
        Assert.Equal(1, _streamCount);
    }

    [Then(@"an empty event stream is returned")]
    public void ThenAnEmptyEventStreamIsReturned()
    {
        Assert.Equal(0, _streamCount);
    }
}
