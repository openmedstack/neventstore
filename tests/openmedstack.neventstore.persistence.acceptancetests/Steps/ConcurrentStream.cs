using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

public partial class PersistenceEngineBehavior
{
    private IEventStream _concurrentAttempt = null!;
    private ICommit _attemptTwice = null!;
    private Exception _thrown = null!;
    private CommitAttempt _secondAttempt = null!;

    [Given(@"a persisted stream")]
    public async Task GivenAPersistedStream()
    {
        var streamId = Guid.NewGuid().ToString();
        var attempt1 = streamId.BuildAttempt();
        await Persistence.Commit(attempt1);
        _concurrentAttempt = await OptimisticEventStream.Create(Bucket.Default, streamId, Persistence);
        foreach (var header in attempt1.CommittedHeaders)
        {
            _concurrentAttempt.Add(header.Key, header.Value);
        }

        _concurrentAttempt.Add(new EventMessage(new ExtensionMethods.SomeDomainEvent { SomeProperty = "Test 3" }));
        attempt1 = attempt1.BuildNextAttempt();
        _ = (await Persistence.Commit(attempt1))!;
    }

    [When(@"committing a stream with the same sequence id")]
    public async Task WhenCommittingAStreamWithTheSameSequenceId()
    {
        _commit = await Persistence.Commit(_concurrentAttempt).ConfigureAwait(false);
    }

    [Then(@"should update to include competing events")]
    public void ThenShouldUpdateToIncludeCompetingEvents()
    {
        Assert.Equal(5, _commit?.StreamRevision);
    }

    [Given(@"an already persisted stream")]
    public async Task GivenAnAlreadyPersistedStream()
    {
        _attemptTwice = (await Persistence.CommitSingle())!;
    }

    [When(@"committing the stream again")]
    public async Task WhenCommittingTheStreamAgain()
    {
        _thrown = (await Catch
            .Exception(() => Persistence.Commit(new CommitAttemptStream(CommitAttempt.FromCommit(_attemptTwice)),
                _attemptTwice.CommitId)))!;
    }

    [Then(@"should throw a duplicate commit exception")]
    public void ThenShouldThrowADuplicateCommitException()
    {
        Assert.IsType<DuplicateCommitException>(_thrown);
    }

    [Given(@"an existing commit attempt")]
    public async Task GivenAnExistingCommitAttempt()
    {
        var commit = (await Persistence.CommitSingle())!;
        _secondAttempt = new CommitAttempt(
            commit.BucketId,
            commit.StreamId,
            commit.StreamRevision + 1,
            commit.CommitId,
            commit.CommitSequence + 1,
            commit.CommitStamp,
            commit.Headers,
            commit.Events.ToList());
    }

    [When(@"committing again on the same stream")]
    public async Task WhenCommittingAgainOnTheSameStream()
    {
        _thrown = (await Catch
            .Exception(() => Persistence.Commit(new CommitAttemptStream(_secondAttempt), _secondAttempt.CommitId))
            .ConfigureAwait(false))!;
    }
}
