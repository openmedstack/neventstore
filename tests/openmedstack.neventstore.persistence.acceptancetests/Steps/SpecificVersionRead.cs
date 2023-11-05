using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

public partial class PersistenceEngineBehavior
{
    private ICommit _oldest = null!, _oldest2 = null!, _oldest3 = null!;

    [Given(@"an event stream with 3 commits")]
    public async Task GivenAnEventStreamWith3Commits()
    {
        _oldest = (await Persistence.CommitSingle().ConfigureAwait(false))!; // 2 events, revision 1-2
        _oldest2 = (await Persistence.CommitNext(_oldest).ConfigureAwait(false))!; // 2 events, revision 3-4
        _oldest3 = (await Persistence.CommitNext(_oldest2).ConfigureAwait(false))!; // 2 events, revision 5-6
        await Persistence.CommitNext(_oldest3).ConfigureAwait(false); // 2 events, revision 7-8

        _streamId = _oldest.StreamId;
    }

    [When(@"getting a range from (\d+) to (\d+)")]
    public async Task WhenGettingASpecificRevision(int from, int to)
    {
        _committed = await Persistence
            .Get(Bucket.Default, _streamId, from, to,
                CancellationToken.None).ToArray().ConfigureAwait(false);
    }

    [Then(@"should start from the commit which contains the minimum stream revision specified")]
    public void ThenShouldStartFromTheCommitWhichContainsTheMinimumStreamRevisionSpecified()
    {
        Assert.Equal(_oldest2.CommitId, _committed.First().CommitId); // contains revision 3
    }

    [Then(@"should read up to the commit which contains the maximum stream revision specified")]
    public void ThenShouldReadUpToTheCommitWhichContainsTheMaximumStreamRevisionSpecified()
    {
        Assert.Equal(_oldest3.CommitId, _committed.Last().CommitId); // contains revision 5
    }
}
