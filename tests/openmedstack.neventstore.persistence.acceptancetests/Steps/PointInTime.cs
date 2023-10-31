using OpenMedStack.NEventStore.Abstractions;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

public partial class PersistenceEngineBehavior
{
    private IEventStream _first = null!;
    private ICommit _second = null!;
    private ICommit _third = null!;

    [Given(@"some streams created now")]
    public async Task GivenSomeStreamsCreatedNow()
    {
        _streamId = Guid.NewGuid().ToString();

        _now = SystemTime.UtcNow; //.AddYears(1);
        _first = _streamId.BuildAttempt();
        var firstCommit = await Persistence.Commit(_first).ConfigureAwait(false);

        _second = (await Persistence.CommitNext(firstCommit!).ConfigureAwait(false))!;
        _third = (await Persistence.CommitNext(_second).ConfigureAwait(false))!;
        await Persistence.CommitNext(_third).ConfigureAwait(false);
    }

    [When(@"getting streams from now onwards")]
    public async Task WhenGettingStreamsFromNowOnwards()
    {
        var enumerable = PersistenceManagement.GetFrom(_now);
        _committed = await enumerable.ToArray().ConfigureAwait(false);
    }

    [Then(@"should return the streams created on or after that point in time")]
    public void ThenShouldReturnTheStreamsCreatedOnOrAfterThatPointInTime()
    {
        Assert.Equal(4, _committed.Length);
    }

    [Given(@"more streams than the page size committed now")]
    public async Task GivenMoreStreamsThanThePageSizeCommittedNow()
    {
        // Due to loss in precision in various storage engines, we're rounding down to the
        // nearest second to ensure include all commits from the 'start'.
        _now = SystemTime.UtcNow.AddSeconds(-1);

        _committed =
            (await Persistence.CommitMany(ConfiguredPageSizeForTesting + 2)).ToArray();
    }

    [When(@"loading streams from now onwards")]
    public async Task WhenLoadingStreamsFromNowOnwards()
    {
        var asyncEnumerable = PersistenceManagement.GetFrom(_now);
        _loaded = await asyncEnumerable.ToArray(CancellationToken.None).ConfigureAwait(false);
    }

    [Then(@"should load the same commits which have been persisted")]
    public void ThenShouldLoadTheSameCommitsWhichHaveBeenPersisted()
    {
        Assert.All(_committed,
            commit => Assert.NotNull(_loaded.SingleOrDefault(loaded => loaded.CommitId == commit.CommitId)));
    }

    [When(@"reading all commits from start")]
    public async Task WhenReadingAllCommitsFromStart()
    {
        _thrown = (await Catch.Exception(
                async () =>
                {
                    var enumerable = PersistenceManagement.GetFrom(DateTimeOffset.MinValue);
                    _ = await enumerable.FirstOrDefault(CancellationToken.None).ConfigureAwait(false);
                })
            .ConfigureAwait(false))!;
    }

    [Then(@"should not throw and exception")]
    public void ThenShouldNotThrowAndException()
    {
        Assert.Null(_thrown);
    }
}
