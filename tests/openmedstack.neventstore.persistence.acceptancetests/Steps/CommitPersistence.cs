using OpenMedStack.NEventStore.Abstractions;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

public partial class PersistenceEngineBehavior
{
    private CommitAttempt _attempt = null!;
    private DateTimeOffset _now;
    private ICommit _persisted = null!;
    private string _streamId = null!;

    [Given(@"a persisted event stream in bucket (.+)")]
    public Task GivenAPersistedEventStreamInBucket(string TenantId)
    {
        _now = SystemTime.UtcNow; //.AddYears(1);
        _streamId = Guid.NewGuid().ToString();
        _attempt = _streamId.BuildAttempt(TenantId);

        return Persistence.Commit(_attempt);
    }

    [Then(@"should correctly persist the stream identifier")]
    public void ThenShouldCorrectlyPersistTheStreamIdentifier()
    {
        Assert.Equal(_attempt.StreamId, _persisted.StreamId);
    }

    [Then(@"should correctly persist the stream revision")]
    public void ThenShouldCorrectlyPersistTheStreamRevision()
    {
        Assert.Equal(_attempt.StreamRevision, _persisted.StreamRevision);
    }

    [Then(@"should correctly persist the commit sequence")]
    public void ThenShouldCorrectlyPersistTheCommitSequence()
    {
        Assert.Equal(_attempt.CommitSequence, _persisted.CommitSequence);
    }

    [Then(@"should correctly persist the commit stamp")]
    public void ThenShouldCorrectlyPersistTheCommitStamp()
    {
        var difference = _persisted.CommitStamp.Subtract(_now);
        Assert.Equal(0, difference.Days);
        Assert.Equal(0, difference.Hours);
        Assert.Equal(0, difference.Minutes);
        Assert.True(difference <= TimeSpan.FromSeconds(1));
    }

    [Then(@"should correctly persist the headers")]
    public void ThenShouldCorrectlyPersistTheHeaders()
    {
        Assert.Equal(_attempt.Headers.Count, _persisted.Headers.Count);
    }

    [Then(@"should correctly persist the events")]
    public void ThenShouldCorrectlyPersistTheEvents()
    {
        Assert.Equal(_attempt.Events.Count, _persisted.Events.Count);
    }

    [Then(@"should cause the stream to be found in the list of streams to snapshot")]
    public async Task ThenShouldCauseTheStreamToBeFoundInTheListOfStreamsToSnapshot()
    {
        var streamHead = PersistenceManagement.GetStreamsToSnapshot("default", 1, CancellationToken.None);
        var head = await streamHead.FirstOrDefault(x => x.StreamId == _streamId, CancellationToken.None).ConfigureAwait(false);
        Assert.NotNull(head);
    }
}
