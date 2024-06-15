using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

public partial class PersistenceEngineBehavior
{
    private const string BucketAId = "A";
    private const string BucketBId = "B";
    private List<Guid> _committedOnBucket1 = null!;
    private List<Guid> _committedOnBucket2 = null!;
    private DateTimeOffset _attemptACommitStamp;
    private CommitAttempt _attemptForBucketB = null!;
    private ICommit _commitToBucketB = null!;
    private ICommit[] _returnedCommits = null!;
    private Exception _thrown = null!;

    [Given(@"multiple streams in different buckets")]
    public async Task GivenMultipleStreamsInDifferentBuckets()
    {
        _committedOnBucket1 =
            (await Persistence.CommitMany(ConfiguredPageSizeForTesting + 1, null, "b1").ConfigureAwait(false))
            .Select(c => c.CommitId).ToList();
        _committedOnBucket2 =
            (await Persistence.CommitMany(ConfiguredPageSizeForTesting + 1, null, "b2").ConfigureAwait(false))
            .Select(c => c.CommitId).ToList();
        _committedOnBucket1.AddRange(
            (await Persistence.CommitMany(4, null, "b1").ConfigureAwait(false)).Select(c => c.CommitId));
    }

    [When(@"getting commits from bucket 1 from a checkpoint")]
    public async Task WhenGettingCommitsFromBucketFromACheckpoint()
    {
        var enumerable = PersistenceManagement.GetFrom("b1", CheckPoint, CancellationToken.None);
        _loadedIds = (await enumerable.ToList().ConfigureAwait(false)).Select(c => c.CommitId).ToArray();
    }

    [Then(
        @"should load the same number of commits from bucket 1 which have been persisted starting from the checkpoint")]
    public void ThenShouldLoadTheSameNumberOfCommitsFromBucketWhichHaveBeenPersistedStartingFromTheCheckpoint()
    {
        Assert.Equal(_committedOnBucket1.Count - CheckPoint, _loadedIds.Length);
    }

    [Then(@"should load only commits from bucket 1 from the checkpoint")]
    public void ThenShouldLoadOnlyCommitsFromBucketFromTheCheckpoint()
    {
        Assert.All(_committedOnBucket1.Skip(CheckPoint), x => Assert.Contains(x, _loadedIds));
    }

    [Then(@"should not load the commits from bucket 2")]
    public void ThenShouldNotLoadTheCommitsFromBucket()
    {
        Assert.All(_committedOnBucket2, x => Assert.DoesNotContain(x, _loadedIds));
    }

    [Given(@"a stream committed in bucket A")]
    public async Task GivenAStreamCommittedInBucketA()
    {
        _streamId = Guid.NewGuid().ToString();
        await Persistence.Commit(_streamId.BuildAttempt(BucketAId)).ConfigureAwait(false);
        var enumerable = Persistence.Get(BucketAId, _streamId, 0, int.MaxValue, CancellationToken.None);
        _attemptACommitStamp =
            (await enumerable.First())
            .CommitStamp;
    }

    [Given(@"a stream to commit to bucket B")]
    public void GivenAStreamToCommitToBucketB()
    {
        _attemptForBucketB = _streamId.BuildAttempt(BucketBId);
    }

    [When(@"committing to bucket B")]
    public async Task WhenCommittingToBucketB()
    {
        _thrown = (await Catch.Exception(() => Persistence.Commit(_attemptForBucketB)).ConfigureAwait(false))!;
    }

    [Then(@"should succeed")]
    public void ThenShouldSucceed()
    {
        Assert.Null(_thrown);
    }

    [Then(@"should persist to the correct bucket")]
    public async Task ThenShouldPersistToTheCorrectBucket()
    {
        var enumerable = Persistence.Get(BucketBId, _streamId, 0, int.MaxValue, CancellationToken.None);
        var stream = await enumerable.ToList();
        Assert.NotNull(stream);
        Assert.Single(stream);
    }

    [Then(@"should not affect the stream from the other bucket")]
    public async Task ThenShouldNotAffectTheStreamFromTheOtherBucket()
    {
        var enumerable = Persistence.Get(BucketAId, _streamId, 0, int.MaxValue, CancellationToken.None);
        var stream = await enumerable.ToList();
        Assert.NotNull(stream);
        Assert.Single(stream);
        Assert.Equal(_attemptACommitStamp, stream.First().CommitStamp);
    }

    [Given(@"(.*) persisted streams in different buckets")]
    public async Task GivenPersistedStreamsInDifferentBuckets(int p0)
    {
        _streamId = Guid.NewGuid().ToString();
        _snapshot = new Snapshot(BucketBId, _streamId, 1, "Snapshot");
        await Persistence.Commit(_streamId.BuildAttempt(bucketId: BucketAId)).ConfigureAwait(false);
        await Persistence.Commit(_streamId.BuildAttempt(bucketId: BucketBId)).ConfigureAwait(false);
    }

    [When(@"saving a snapshot for the stream in bucket B")]
    public async Task WhenSavingASnapshotForTheStreamInBucketB()
    {
        var result = await Snapshots.AddSnapshot(_snapshot);

        Assert.True(result);
    }

    [Then(@"not affect snapshots in bucket A")]
    public async Task ThenNotAffectSnapshotsInBucketA()
    {
        var snapshot = await Snapshots
            .GetSnapshot(BucketAId, _streamId, _snapshot.StreamRevision, CancellationToken.None);
        Assert.Null(snapshot);
    }

    [Given(@"streams committed to multiple buckets")]
    public async Task GivenStreamsCommittedToMultipleBuckets()
    {
        _now = SystemTime.UtcNow.AddYears(1);

        var commitToBucketA = Guid.NewGuid().ToString().BuildAttempt();

        await Persistence.Commit(commitToBucketA).ConfigureAwait(false);
        await Persistence.Commit(commitToBucketA = commitToBucketA.BuildNextAttempt()).ConfigureAwait(false);
        await Persistence.Commit(commitToBucketA = commitToBucketA.BuildNextAttempt()).ConfigureAwait(false);
        await Persistence.Commit(commitToBucketA.BuildNextAttempt()).ConfigureAwait(false);

        var stream = Guid.NewGuid().ToString().BuildAttempt(BucketBId);

        _commitToBucketB = (await Persistence.Commit(stream))!;
    }

    [When(@"getting commits from bucket A")]
    public async Task WhenGettingCommitsFromBucketA()
    {
        var enumerable = PersistenceManagement.GetFrom(BucketAId, 0, CancellationToken.None);
        _returnedCommits = await enumerable.ToArray().ConfigureAwait(false);
    }

    [Then(@"should not return commits from other buckets")]
    public void ThenShouldNotReturnCommitsFromOtherBuckets()
    {
        Assert.DoesNotContain(_returnedCommits, c => c.CommitId.Equals(_commitToBucketB.CommitId));
    }

    [Given(@"streams committed to buckets A and B")]
    public async Task GivenStreamsCommittedToBucketsAAndB()
    {
        await Persistence.Commit(Guid.NewGuid().ToString().BuildAttempt(bucketId: BucketAId)).ConfigureAwait(false);
        await Persistence.Commit(Guid.NewGuid().ToString().BuildAttempt(bucketId: BucketBId)).ConfigureAwait(false);
        await Persistence.Commit(Guid.NewGuid().ToString().BuildAttempt(bucketId: BucketAId)).ConfigureAwait(false);
    }

    [When(@"getting all commits from bucket A")]
    public async Task WhenGettingAllCommitsFromBucketA()
    {
        var enumerable = PersistenceManagement.GetFromStart("A");
        _returnedCommits = await enumerable.ToArray().ConfigureAwait(false);
    }

    [Then(@"has commits from bucket A")]
    public void ThenHasCommitsFromBucketA()
    {
        Assert.NotEmpty(_returnedCommits);
    }

    [Then(@"is returned in order of checkpoint")]
    public void ThenIsReturnedInOrderOfCheckpoint()
    {
        long checkpoint = 0;
        foreach (var commit in _returnedCommits)
        {
            var commitCheckpoint = commit.CheckpointToken;
            Assert.True(commitCheckpoint > checkpoint);
            checkpoint = commit.CheckpointToken;
        }
    }

    private const int BodyLength = 100000;

    [When(@"committing stream with a large payload")]
    public async Task WhenCommittingStreamWithALargePayload()
    {
        _streamId = Guid.NewGuid().ToString();
        var stream = new CommitAttempt("default", _streamId, 1, Guid.NewGuid(), 1, DateTimeOffset.UtcNow,
            new Dictionary<string, object>(), [new EventMessage(new string('a', BodyLength))]);
        await Persistence.Commit(stream);
    }

    [Then(@"reads the whole body")]
    public async Task ThenReadsTheWholeBody()
    {
        var commits = await Persistence
            .Get("default", _streamId, 0, int.MaxValue, CancellationToken.None).Single();
        Assert.Equal(BodyLength, commits.Events.Single().Body.ToString()!.Length);
    }
}
