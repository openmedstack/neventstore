using OpenMedStack.NEventStore.Abstractions;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

public partial class PersistenceEngineBehavior
{
    private ICommit[] _loaded = null!;

    [Given(@"more commits than the page size are persisted")]
    public async Task GivenMoreCommitsThanThePageSizeArePersisted()
    {
        _streamId = Guid.NewGuid().ToString();
        _committed =
            (await Persistence.CommitMany(ConfiguredPageSizeForTesting + 2, _streamId).ConfigureAwait(false))
            .ToArray();
    }

    [When(@"loading all committed events")]
    public async Task WhenLoadingAllCommittedEvents()
    {
        _loaded = await Persistence.Get("default", _streamId, 0, int.MaxValue, CancellationToken.None)
            .ToArray()
            .ConfigureAwait(false);
    }

    [Then(@"should load the same number of commits which have been persisted")]
    public void ThenShouldLoadTheSameNumberOfCommitsWhichHaveBeenPersisted()
    {
        Assert.Equal(_committed.Length, _loaded.Length);
    }

    [Then(@"should have the same commits as those which have been persisted")]
    public void ThenShouldHaveTheSameCommitsAsThoseWhichHaveBeenPersisted()
    {
        Assert.All(_committed,
            commit => Assert.Contains(_loaded, loaded => loaded.CommitId == commit.CommitId));
    }
}
