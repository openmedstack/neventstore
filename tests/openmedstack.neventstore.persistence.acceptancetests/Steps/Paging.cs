using OpenMedStack.NEventStore.Abstractions;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

public partial class PersistenceEngineBehavior
{
    private const int CheckPoint = 2;
    private Guid[] _committedIds = [];
    private Guid[] _loadedIds = [];
    private int _moreThanPageSize;

    [Given(@"one more committed stream than page size")]
    public async Task GivenOneMoreCommittedStreamThanPageSize()
    {
        _committedIds = (await Persistence.CommitMany(ConfiguredPageSizeForTesting + 1).ConfigureAwait(false))
            .Select(c => c.CommitId).ToArray();
    }

    [When(@"loading streams from a checkpoint")]
    public async Task WhenLoadingStreamsFromACheckpoint()
    {
        var enumerable = PersistenceManagement.GetFrom("default", CheckPoint, CancellationToken.None);
        _loadedIds = (await enumerable.ToList().ConfigureAwait(false)).Select(c => c.CommitId).ToArray();
    }

    [Then(@"should load the same number of commits which have been persisted starting from the checkpoint")]
    public void ThenShouldLoadTheSameNumberOfCommitsWhichHaveBeenPersistedStartingFromTheCheckpoint()
    {
        Assert.Equal(_committedIds.Length - CheckPoint, _loadedIds.Length);
    }

    [Then(@"should load only the commits starting from the checkpoint")]
    public void ThenShouldLoadOnlyTheCommitsStartingFromTheCheckpoint()
    {
        Assert.All(_committedIds.Skip(CheckPoint), x => Assert.Contains(x, _loadedIds));
    }

    [Given(@"more streams persisted than the page size")]
    public async Task GivenMoreStreamsPersistedThanThePageSize()
    {
        _moreThanPageSize = ConfiguredPageSizeForTesting + 1;

        for (var i = 0; i < _moreThanPageSize; i++)
        {
            var stream = new CommitAttempt("default", Guid.NewGuid().ToString("N"), 1, Guid.NewGuid(), 1,
                SystemTime.UtcNow,
                new Dictionary<string, object>(), [
                    new EventMessage(new Pippo { S = "Hi " + i })
                ]);
            await Persistence.Commit(stream);
        }
    }

    [When(@"getting all commits")]
    public async Task WhenGettingAllCommits()
    {
        var asyncEnumerable = PersistenceManagement.GetFrom("default", 0, CancellationToken.None);
        _returnedCommits = await asyncEnumerable.ToArray().ConfigureAwait(false);
    }

    [Then(@"should have expected number of commits")]
    public void ThenShouldHaveExpectedNumberOfCommits()
    {
        Assert.Equal(_moreThanPageSize, _returnedCommits.Length);
    }
}

public class Pippo
{
    public string S { get; set; } = null!;
}
