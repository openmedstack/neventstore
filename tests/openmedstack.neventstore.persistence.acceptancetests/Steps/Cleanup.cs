using OpenMedStack.NEventStore.Abstractions;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

public partial class PersistenceEngineBehavior
{
    [When(@"purging all streams and commits in bucket (.+)")]
    public async Task WhenPurgingAllStreamsAndCommits(string bucketId)
    {
        await PersistenceManagement.Purge(bucketId);
    }

    [Then(@"should not find any commits stored in bucket (.+)")]
    public async Task ThenShouldNotFindAnyCommitsStoredInBucket(string bucketId)
    {
        var enumerable = PersistenceManagement.GetFrom(bucketId, 0, CancellationToken.None);
        Assert.Empty(await enumerable.ToList(CancellationToken.None));
    }

    [Then(@"should not find any streams to snapshot in bucket (.+)")]
    public async Task ThenShouldNotFindAnyStreamsToSnapshotInBucket(string bucketId)
    {
        Assert.Empty(
            await PersistenceManagement.GetStreamsToSnapshot(bucketId, 0, CancellationToken.None).ToList());
    }

    [When(@"the storage is disposed")]
    public void WhenTheStorageIsDisposed()
    {
        Persistence.Dispose();
    }

    [When(@"making a commit")]
    public async Task WhenMakingACommit()
    {
        _thrown = (await Catch.Exception(() => Persistence.CommitSingle()).ConfigureAwait(false))!;
    }

    [Then(@"should throw a disposed exception")]
    public void ThenShouldThrowADisposedException()
    {
        Assert.IsType<ObjectDisposedException>(_thrown);
    }

    [Given(@"event streams persisted in different buckets")]
    public async Task GivenEventStreamsPersistedInDifferentBuckets()
    {
        _streamId = Guid.NewGuid().ToString();
        await Persistence.Commit(_streamId.BuildAttempt(bucketId: BucketAId)).ConfigureAwait(false);
        await Persistence.Commit(_streamId.BuildAttempt(bucketId: BucketBId)).ConfigureAwait(false);
    }

    [Then(@"should purge all commits stored in bucket (.+)")]
    public async Task ThenShouldPurgeAllCommitsStoredInBucket(string bucketId)
    {
        var asyncEnumerable = PersistenceManagement.GetFrom(bucketId, 0, CancellationToken.None);
        Assert.Equal(0, await asyncEnumerable.Count());
    }

    [Then(@"should purge all streams to snapshot in bucket (.+)")]
    public async Task ThenShouldPurgeAllStreamsToSnapshotInBucketA(string bucketId)
    {
        Assert.Equal(0, await PersistenceManagement
            .GetStreamsToSnapshot(bucketId, 0, CancellationToken.None).Count());
    }
}
