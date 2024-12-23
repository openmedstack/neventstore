using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

public partial class PersistenceEngineBehavior
{
    private const int WithinThreshold = 1;
    private const int OverThreshold = 2;

    private const string SnapshotData = "snapshot";
    private ICommit _newest = null!;
    private bool _added = false;
    private ISnapshot _snapshot = null!;
    private ISnapshot _correct = null!;
    private ISnapshot _tooFarForward = null!;

    [Given(@"a non-snapshotted event stream")]
    public async Task GivenANonSnapshottedEventStream()
    {
        _streamId = Guid.NewGuid().ToString();
        _snapshot = new Snapshot("default", _streamId, 1, "Snapshot");
        await Persistence.CommitSingle(_streamId).ConfigureAwait(false);
    }

    [When(@"saving a snapshot")]
    public async Task WhenSavingASnapshot()
    {
        _added = await Snapshots.AddSnapshot(_snapshot).ConfigureAwait(false);
    }

    [Then(@"should save the snapshot")]
    public void ThenShouldSaveTheSnapshot()
    {
        Assert.True(_added);
    }

    [Then(@"should be able to load the snapshot")]
    public async Task ThenShouldBeAbleToLoadTheSnapshot()
    {
        var snapshot =
            await Snapshots.GetSnapshot("default", _streamId, _snapshot.StreamRevision, CancellationToken.None).ConfigureAwait(false);
        Assert.NotNull(snapshot);
    }

    [Given(@"an event stream with snapshots")]
    public async Task GivenAnEventStreamWithSnapshots()
    {
        _streamId = Guid.NewGuid().ToString();
        var commit1 = (await Persistence.CommitSingle(_streamId).ConfigureAwait(false))!; // rev 1-2
        var commit2 = (await Persistence.CommitNext(commit1).ConfigureAwait(false))!; // rev 3-4
        await Persistence.CommitNext(commit2).ConfigureAwait(false); // rev 5-6

        await Snapshots.AddSnapshot(new Snapshot("default", _streamId, 1, string.Empty))
            .ConfigureAwait(false); //Too far back
        await Snapshots.AddSnapshot(_correct = new Snapshot("default", _streamId, 3, "Snapshot"))
            .ConfigureAwait(false);
        await Snapshots.AddSnapshot(_tooFarForward = new Snapshot("default", _streamId, 5, string.Empty))
            .ConfigureAwait(false);
    }

    [When(@"loading a snapshot to far ahead")]
    public async Task WhenLoadingASnapshotToFarAhead()
    {
        _snapshot = (await Snapshots
            .GetSnapshot("default", _streamId, _tooFarForward.StreamRevision - 1, CancellationToken.None)
            .ConfigureAwait(false))!;
    }

    [Then(@"should return the most recent prior snapshot")]
    public void ThenShouldReturnTheMostRecentPriorSnapshot()
    {
        Assert.Equal(_correct.StreamRevision, _snapshot.StreamRevision);
    }

    [Then(@"should have the correct payload")]
    public void ThenShouldHaveTheCorrectPayload()
    {
        Assert.Equal(_correct.Payload, _snapshot.Payload);
    }

    [Then(@"should have the correct stream id")]
    public void ThenShouldHaveTheCorrectStreamId()
    {
        Assert.Equal(_correct.StreamId, _snapshot.StreamId);
    }

    [Given(@"multiple committed streams")]
    public async Task GivenMultipleCommittedStreams()
    {
        _streamId = Guid.NewGuid().ToString();
        _oldest = (await Persistence.CommitSingle(_streamId).ConfigureAwait(false))!;
        _oldest2 = (await Persistence.CommitNext(_oldest).ConfigureAwait(false))!;
        _newest = (await Persistence.CommitNext(_oldest2).ConfigureAwait(false))!;
    }

    [When(@"adding a snapshot to the most recent commit")]
    public void WhenAddingASnapshotToTheMostRecentCommit()
    {
        Snapshots.AddSnapshot(new Snapshot("default", _streamId, _newest.StreamRevision, SnapshotData));
    }

    [Then(@"should no longer find the stream in the set of streams to be snapshotted")]
    public async Task ThenShouldNoLongerFindTheStreamInTheSetOfStreamsToBeSnapshotted()
    {
        Assert.DoesNotContain(
            await PersistenceManagement.GetStreamsToSnapshot("default", 1, CancellationToken.None).ToList().ConfigureAwait(false),
            x => x.StreamId == _streamId);
    }

    [Given(@"a snapshotted event stream")]
    public async Task GivenASnapshottedEventStream()
    {
        _streamId = Guid.NewGuid().ToString();
        _oldest = (await Persistence.CommitSingle(_streamId).ConfigureAwait(false))!;
        _oldest2 = (await Persistence.CommitNext(_oldest).ConfigureAwait(false))!;
        await Snapshots.AddSnapshot(new Snapshot("default", _streamId, _oldest2.StreamRevision, SnapshotData))
            .ConfigureAwait(false);
    }

    [When(@"adding commit after the snapshot")]
    public async Task WhenAddingCommitAfterTheSnapshot()
    {
        await Persistence.Commit(_oldest2.BuildNextAttempt()).ConfigureAwait(false);
    }

    [Then(@"should find the stream in the set of streams to be snapshotted when within the snapshot threshold")]
    public async Task ThenShouldFindTheStreamInTheSetOfStreamsToBeSnapshottedWhenWithinTheSnapshotThreshold()
    {
        var value = await PersistenceManagement
            .GetStreamsToSnapshot("default", WithinThreshold, CancellationToken.None)
            .FirstOrDefault(x => x.StreamId == _streamId).ConfigureAwait(false);
        Assert.NotNull(value);
    }

    [Then(@"should not find the stream in the set of streams to be snapshotted when outside the snapshot threshold")]
    public async Task ThenShouldNotFindTheStreamInTheSetOfStreamsToBeSnapshottedWhenOutsideTheSnapshotThreshold()
    {
        Assert.DoesNotContain(await PersistenceManagement
            .GetStreamsToSnapshot("default", OverThreshold, CancellationToken.None)
            .ToList().ConfigureAwait(false), x => x.StreamId == _streamId);
    }
}
