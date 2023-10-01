using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore;
using OpenMedStack.NEventStore.Persistence;
using OpenMedStack.NEventStore.Persistence.AcceptanceTests.BDD;
using Xunit;

public class WhenACommitHeaderHasANameThatContainsAPeriod : PersistenceEngineConcern
{
    private ICommit _persisted = null!;
    private string _streamId = null!;

    protected override Task Context()
    {
        _streamId = Guid.NewGuid().ToString();
        var attempt = OptimisticEventStream.Create(
            Bucket.Default,
            _streamId, NullLogger<OptimisticEventStream>.Instance);
        attempt.Add(new EventMessage(new ExtensionMethods.SomeDomainEvent { SomeProperty = "Test" }));
        attempt.UncommittedHeaders.Add("key.1", "value");

        return Persistence.Commit(attempt);
    }

    protected override async Task Because()
    {
        _persisted = await Persistence.GetFrom(_streamId, 0, int.MaxValue, CancellationToken.None).First()
            .ConfigureAwait(false);
    }

    [Fact]
    public void should_correctly_deserialize_headers()
    {
        Assert.Contains("key.1", _persisted.Headers.Keys);
    }
}

public class WhenACommitIsSuccessfullyPersisted : PersistenceEngineConcern
{
    private IEventStream _attempt = null!;
    private DateTimeOffset _now;
    private ICommit _persisted = null!;
    private string _streamId = null!;

    protected override Task Context()
    {
        _now = SystemTime.UtcNow; //.AddYears(1);
        _streamId = Guid.NewGuid().ToString();
        _attempt = _streamId.BuildAttempt();

        return Persistence.Commit(_attempt);
    }

    protected override async Task Because()
    {
        _persisted = await Persistence.GetFrom(_streamId, 0, int.MaxValue, CancellationToken.None).First()
            .ConfigureAwait(false);
    }

    [Fact]
    public void should_correctly_persist_the_stream_identifier()
    {
        Assert.Equal(_attempt.StreamId, _persisted.StreamId);
    }

    [Fact]
    public void should_correctly_persist_the_stream_stream_revision()
    {
        Assert.Equal(_attempt.StreamRevision, _persisted.StreamRevision);
    }

    [Fact]
    public void should_correctly_persist_the_commit_sequence()
    {
        Assert.Equal(_attempt.CommitSequence + 1, _persisted.CommitSequence);
    }

    // persistence engines have varying levels of precision with respect to time.
    [Fact]
    public void should_correctly_persist_the_commit_stamp()
    {
        var difference = _persisted.CommitStamp.Subtract(_now);
        Assert.Equal(0, difference.Days);
        Assert.Equal(0, difference.Hours);
        Assert.Equal(0, difference.Minutes);
        Assert.True(difference <= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void should_correctly_persist_the_headers()
    {
        Assert.Equal(_attempt.UncommittedHeaders.Count, _persisted.Headers.Count);
    }

    [Fact]
    public void should_correctly_persist_the_events()
    {
        Assert.Equal(_attempt.UncommittedEvents.Count, _persisted.Events.Count);
    }

    [Fact]
    public async Task should_cause_the_stream_to_be_found_in_the_list_of_streams_to_snapshot()
    {
        var streamHead = Persistence.GetStreamsToSnapshot(1, CancellationToken.None);
        Assert.NotNull(await streamHead.FirstOrDefault(x => x.StreamId == _streamId, CancellationToken.None));
    }
}

public class WhenReadingFromAGivenRevision : PersistenceEngineConcern
{
    private const int LoadFromCommitContainingRevision = 3;
    private const int UpToCommitWithContainingRevision = 5;
    private ICommit[] _committed = null!;
    private ICommit _oldest = null!, _oldest2 = null!, _oldest3 = null!;
    private string _streamId = null!;

    protected override async Task Context()
    {
        _oldest = (await Persistence.CommitSingle().ConfigureAwait(false))!; // 2 events, revision 1-2
        _oldest2 = (await Persistence.CommitNext(_oldest).ConfigureAwait(false))!; // 2 events, revision 3-4
        _oldest3 = (await Persistence.CommitNext(_oldest2).ConfigureAwait(false))!; // 2 events, revision 5-6
        await Persistence.CommitNext(_oldest3).ConfigureAwait(false); // 2 events, revision 7-8

        _streamId = _oldest.StreamId;
    }

    protected override async Task Because()
    {
        _committed = await Persistence
            .GetFrom(_streamId, LoadFromCommitContainingRevision, UpToCommitWithContainingRevision,
                CancellationToken.None).ToArray().ConfigureAwait(false);
    }

    [Fact]
    public void should_start_from_the_commit_which_contains_the_min_stream_revision_specified()
    {
        Assert.Equal(_oldest2.CommitId, _committed.First().CommitId); // contains revision 3
    }

    [Fact]
    public void should_read_up_to_the_commit_which_contains_the_max_stream_revision_specified()
    {
        Assert.Equal(_oldest3.CommitId, _committed.Last().CommitId); // contains revision 5
    }
}

public class WhenReadingFromAGivenRevisionToCommitRevision : PersistenceEngineConcern
{
    private const int LoadFromCommitContainingRevision = 3;
    private const int UpToCommitWithContainingRevision = 6;
    private ICommit[] _committed = null!;
    private ICommit _oldest = null!, _oldest2 = null!, _oldest3 = null!;
    private string _streamId = null!;

    protected override async Task Context()
    {
        _oldest = (await Persistence.CommitSingle().ConfigureAwait(false))!; // 2 events, revision 1-2
        _oldest2 = (await Persistence.CommitNext(_oldest).ConfigureAwait(false))!; // 2 events, revision 3-4
        _oldest3 = (await Persistence.CommitNext(_oldest2).ConfigureAwait(false))!; // 2 events, revision 5-6
        await Persistence.CommitNext(_oldest3).ConfigureAwait(false); // 2 events, revision 7-8

        _streamId = _oldest.StreamId;
    }

    protected override async Task Because()
    {
        _committed = await Persistence
            .GetFrom(_streamId, LoadFromCommitContainingRevision, UpToCommitWithContainingRevision,
                CancellationToken.None).ToArray().ConfigureAwait(false);
    }

    [Fact]
    public void should_start_from_the_commit_which_contains_the_min_stream_revision_specified()
    {
        Assert.Equal(_oldest2.CommitId, _committed.First().CommitId); // contains revision 3
    }

    [Fact]
    public void should_read_up_to_the_commit_which_contains_the_max_stream_revision_specified()
    {
        Assert.Equal(_oldest3.CommitId, _committed.Last().CommitId); // contains revision 6
    }
}

public class WhenCommittingAStreamWithTheSameRevision : PersistenceEngineConcern
{
    private IEventStream _attemptWithSameRevision = null!;
    private Exception _thrown = null!;

    protected override async Task Context()
    {
        var commit = (await Persistence.CommitSingle().ConfigureAwait(false))!;
        _attemptWithSameRevision = commit.StreamId.BuildAttempt();
    }

    protected override async Task Because()
    {
        _thrown = (await Catch.Exception(() => Persistence.Commit(_attemptWithSameRevision))
            .ConfigureAwait(false))!;
    }

    [Fact]
    public void should_throw_a_ConcurrencyException()
    {
        Assert.IsType<ConcurrencyException>(_thrown);
    }
}

// This test ensure the uniqueness of BucketId+StreamId+CommitSequence
// to avoid concurrency issues
public class WhenCommittingAStreamWithTheSameSequenceId : PersistenceEngineConcern
{
    private IEventStream _concurrentAttempt = null!;
    private Exception _thrown = null!;

    protected override async Task Context()
    {
        var streamId = Guid.NewGuid().ToString();
        var attempt1 = streamId.BuildAttempt();
        await Persistence.Commit(attempt1);
        attempt1 = attempt1.BuildNextAttempt();
        var commit = (await Persistence.Commit(attempt1))!;
        _concurrentAttempt = new CommitAttemptStream(new CommitAttempt(
            commit.BucketId, // <--- Same bucket
            commit.StreamId, // <--- Same stream it
            commit.StreamRevision + 10,
            Guid.NewGuid(),
            commit.CommitSequence - 1, // <--- Same commit seq
            DateTime.UtcNow,
            attempt1.CommittedHeaders,
            new[]
            {
                new EventMessage(new ExtensionMethods.SomeDomainEvent { SomeProperty = "Test 3" })
            }
        ));
    }

    protected override async Task Because()
    {
        _thrown = (await Catch.Exception(() => Persistence.Commit(_concurrentAttempt)).ConfigureAwait(false))!;
    }

    [Fact]
    public void should_throw_a_ConcurrencyException()
    {
        Assert.IsType<ConcurrencyException>(_thrown);
    }
}

//TODO:This test looks exactly like the one above. What are we trying to prove?
public class WhenAttemptingToOverwriteACommittedSequence : PersistenceEngineConcern
{
    private IEventStream _failedAttempt = null!;
    private Exception _thrown = null!;

    protected override async Task Context()
    {
        var streamId = Guid.NewGuid().ToString();
        var successfulAttempt = streamId.BuildAttempt();
        await Persistence.Commit(successfulAttempt).ConfigureAwait(false);
        _failedAttempt = streamId.BuildAttempt();
    }

    protected override async Task Because()
    {
        _thrown = (await Catch.Exception(() => Persistence.Commit(_failedAttempt)).ConfigureAwait(false))!;
    }

    [Fact]
    public void should_throw_a_ConcurrencyException()
    {
        Assert.IsType<ConcurrencyException>(_thrown);
    }
}

public class WhenAttemptingToPersistACommitTwice : PersistenceEngineConcern
{
    private ICommit _attemptTwice = null!;
    private Exception _thrown = null!;

    protected override async Task Context()
    {
        _attemptTwice = (await Persistence.CommitSingle())!;
    }

    protected override async Task Because()
    {
        _thrown = (await Catch
            .Exception(() => Persistence.Commit(new CommitAttemptStream(CommitAttempt.FromCommit(_attemptTwice)), _attemptTwice.CommitId)))!;
    }

    [Fact]
    public void should_throw_a_DuplicateCommitException()
    {
        Assert.IsType<DuplicateCommitException>(_thrown);
    }
}

public class WhenAttemptingToPersistACommitIdTwiceOnSameStream : PersistenceEngineConcern
{
    private CommitAttempt _attemptTwice = null!;
    private Exception _thrown = null!;

    protected override async Task Context()
    {
        var commit = (await Persistence.CommitSingle())!;
        _attemptTwice = new CommitAttempt(
            commit.BucketId,
            commit.StreamId,
            commit.StreamRevision + 1,
            commit.CommitId,
            commit.CommitSequence + 1,
            commit.CommitStamp,
            commit.Headers,
            commit.Events.ToList());
    }

    protected override async Task Because()
    {
        _thrown = (await Catch
            .Exception(() => Persistence.Commit(new CommitAttemptStream(_attemptTwice), _attemptTwice.CommitId))
            .ConfigureAwait(false))!;
    }

    [Fact]
    public void should_throw_a_DuplicateCommitException()
    {
        Assert.IsType<DuplicateCommitException>(_thrown);
    }
}

public class WhenCommittingMoreEventsThanTheConfiguredPageSize : PersistenceEngineConcern
{
    private ICommit[] _committed = null!;
    private ICommit[] _loaded = null!;
    private string _streamId = null!;

    protected override async Task Context()
    {
        _streamId = Guid.NewGuid().ToString();
        _committed =
            (await Persistence.CommitMany(ConfiguredPageSizeForTesting + 2, _streamId))
            .ToArray();
    }

    protected override async Task Because()
    {
        _loaded = await Persistence.GetFrom(_streamId, 0, int.MaxValue, CancellationToken.None).ToArray()
            .ConfigureAwait(false);
    }

    [Fact]
    public void should_load_the_same_number_of_commits_which_have_been_persisted()
    {
        Assert.Equal(_committed.Length, _loaded.Length);
    }

    [Fact]
    public void should_load_the_same_commits_which_have_been_persisted()
    {
        Assert.All(_committed,
            commit => Assert.True(_loaded.SingleOrDefault(loaded => loaded.CommitId == commit.CommitId) != null));
    }
}

public class WhenSavingASnapshot : PersistenceEngineConcern
{
    private bool _added;
    private Snapshot _snapshot = null!;
    private string _streamId = null!;

    protected override async Task Context()
    {
        _streamId = Guid.NewGuid().ToString();
        _snapshot = new Snapshot(_streamId, 1, "Snapshot");
        await Persistence.CommitSingle(_streamId).ConfigureAwait(false);
    }

    protected override async Task Because()
    {
        _added = await Persistence.AddSnapshot(_snapshot).ConfigureAwait(false);
    }

    [Fact]
    public void should_indicate_the_snapshot_was_added()
    {
        Assert.True(_added);
    }

    [Fact]
    public void should_be_able_to_retrieve_the_snapshot()
    {
        Assert.NotNull(Persistence.GetSnapshot(_streamId, _snapshot.StreamRevision, CancellationToken.None));
    }
}

public class WhenRetrievingASnapshot : PersistenceEngineConcern
{
    private ISnapshot _correct = null!;
    private ISnapshot _snapshot = null!;
    private string _streamId = null!;
    private ISnapshot _tooFarForward = null!;

    protected override async Task Context()
    {
        _streamId = Guid.NewGuid().ToString();
        var commit1 = (await Persistence.CommitSingle(_streamId).ConfigureAwait(false))!; // rev 1-2
        var commit2 = (await Persistence.CommitNext(commit1).ConfigureAwait(false))!; // rev 3-4
        await Persistence.CommitNext(commit2).ConfigureAwait(false); // rev 5-6

        await Persistence.AddSnapshot(new Snapshot(_streamId, 1, string.Empty))
            .ConfigureAwait(false); //Too far back
        await Persistence.AddSnapshot(_correct = new Snapshot(_streamId, 3, "Snapshot")).ConfigureAwait(false);
        await Persistence.AddSnapshot(_tooFarForward = new Snapshot(_streamId, 5, string.Empty))
            .ConfigureAwait(false);
    }

    protected override async Task Because()
    {
        _snapshot = (await Persistence
            .GetSnapshot(_streamId, _tooFarForward.StreamRevision - 1, CancellationToken.None)
            .ConfigureAwait(false))!;
    }

    [Fact]
    public void should_load_the_most_recent_prior_snapshot()
    {
        Assert.Equal(_correct.StreamRevision, _snapshot.StreamRevision);
    }

    [Fact]
    public void should_have_the_correct_snapshot_payload()
    {
        Assert.Equal(_correct.Payload, _snapshot.Payload);
    }

    [Fact]
    public void should_have_the_correct_stream_id()
    {
        Assert.Equal(_correct.StreamId, _snapshot.StreamId);
    }
}

public class WhenASnapshotHasBeenAddedToTheMostRecentCommitOfAStream : PersistenceEngineConcern
{
    private const string SnapshotData = "snapshot";
    private ICommit _newest = null!;
    private ICommit _oldest = null!, _oldest2 = null!;
    private string _streamId = null!;

    protected override async Task Context()
    {
        _streamId = Guid.NewGuid().ToString();
        _oldest = (await Persistence.CommitSingle(_streamId).ConfigureAwait(false))!;
        _oldest2 = (await Persistence.CommitNext(_oldest).ConfigureAwait(false))!;
        _newest = (await Persistence.CommitNext(_oldest2).ConfigureAwait(false))!;
    }

    protected override Task Because() =>
        Persistence.AddSnapshot(new Snapshot(_streamId, _newest.StreamRevision, SnapshotData));

    [Fact]
    public async Task should_no_longer_find_the_stream_in_the_set_of_streams_to_be_snapshot()
    {
        Assert.DoesNotContain(
            await Persistence.GetStreamsToSnapshot(1, CancellationToken.None).ToList(),
            x => x.StreamId == _streamId);
    }
}

public class WhenAddingACommitAfterASnapshot : PersistenceEngineConcern
{
    private const int WithinThreshold = 2;
    private const int OverThreshold = 3;
    private const string SnapshotData = "snapshot";
    private ICommit _oldest = null!, _oldest2 = null!;
    private string _streamId = null!;

    protected override async Task Context()
    {
        _streamId = Guid.NewGuid().ToString();
        _oldest = (await Persistence.CommitSingle(_streamId).ConfigureAwait(false))!;
        _oldest2 = (await Persistence.CommitNext(_oldest).ConfigureAwait(false))!;
        await Persistence.AddSnapshot(new Snapshot(_streamId, _oldest2.StreamRevision, SnapshotData))
            .ConfigureAwait(false);
    }

    protected override async Task Because() => await Persistence.Commit(await _oldest2.BuildNextAttempt());

    // Because Raven and Mongo update the stream head asynchronously, occasionally will fail this test
    [Fact]
    public async Task should_find_the_stream_in_the_set_of_streams_to_be_snapshot_when_within_the_threshold()
    {
        Assert.NotNull(
            (await Persistence.GetStreamsToSnapshot(WithinThreshold, CancellationToken.None).ToList()).FirstOrDefault(
                x => x.StreamId == _streamId));
    }

    [Fact]
    public async Task should_not_find_the_stream_in_the_set_of_streams_to_be_snapshot_when_over_the_threshold()
    {
        Assert.DoesNotContain(await Persistence.GetStreamsToSnapshot(OverThreshold, CancellationToken.None)
            .ToList(), x => x.StreamId == _streamId);
    }
}

public class WhenReadingAllCommitsFromAParticularPointInTime : PersistenceEngineConcern
{
    private ICommit[] _committed = null!;
    private IEventStream _first = null!;
    private DateTimeOffset _now;
    private ICommit _second = null!;
    private string _streamId = null!;
    private ICommit _third = null!;

    protected override async Task Context()
    {
        _streamId = Guid.NewGuid().ToString();

        _now = SystemTime.UtcNow; //.AddYears(1);
        _first = _streamId.BuildAttempt();
        var firstCommit = await Persistence.Commit(_first).ConfigureAwait(false);

        _second = (await Persistence.CommitNext(firstCommit!).ConfigureAwait(false))!;
        _third = (await Persistence.CommitNext(_second).ConfigureAwait(false))!;
        await Persistence.CommitNext(_third).ConfigureAwait(false);
    }

    protected override async Task Because()
    {
        var enumerable = Persistence.GetFrom(_now);
        _committed = await enumerable.ToArray().ConfigureAwait(false);
    }

    [Fact]
    public void should_return_all_commits_on_or_after_the_point_in_time_specified()
    {
        Assert.Equal(4, _committed.Length);
    }
}

public class WhenPagingOverAllCommitsFromAParticularPointInTime : PersistenceEngineConcern
{
    private ICommit[] _committed = null!;
    private List<ICommit> _loaded = null!;
    private DateTimeOffset _start;

    protected override async Task Context()
    {
        _start = SystemTime.UtcNow;
        // Due to loss in precision in various storage engines, we're rounding down to the
        // nearest second to ensure include all commits from the 'start'.
        _start = _start.AddSeconds(-1);
        _committed =
            (await Persistence.CommitMany(ConfiguredPageSizeForTesting + 2)).ToArray();
    }

    protected override async Task Because()
    {
        var asyncEnumerable = Persistence.GetFrom(_start);
        _loaded = await asyncEnumerable.ToList(CancellationToken.None).ConfigureAwait(false);
    }

    [Fact]
    public void should_load_the_same_number_of_commits_which_have_been_persisted()
    {
        Assert.Equal(_committed.Length, _loaded.Count);
    }

    [Fact]
    public void should_load_the_same_commits_which_have_been_persisted()
    {
        Assert.All(_committed,
            commit => Assert.NotNull(_loaded.SingleOrDefault(loaded => loaded.CommitId == commit.CommitId)));
    }
}

public class WhenPagingOverAllCommitsFromAParticularCheckpoint : PersistenceEngineConcern
{
    private List<Guid> _committed = null!;
    private ICollection<Guid> _loaded = null!;
    private const int CheckPoint = 2;

    protected override async Task Context()
    {
        _committed = (await Persistence.CommitMany(ConfiguredPageSizeForTesting + 1).ConfigureAwait(false))
            .Select(c => c.CommitId).ToList();
    }

    protected override async Task Because()
    {
        var enumerable = Persistence.GetFrom(Bucket.Default, CheckPoint, CancellationToken.None);
        _loaded = (await enumerable.ToList().ConfigureAwait(false)).Select(c => c.CommitId).ToList();
    }

    [Fact]
    public void should_load_the_same_number_of_commits_which_have_been_persisted_starting_from_the_checkpoint()
    {
        Assert.Equal(_committed.Count - CheckPoint, _loaded.Count);
    }

    [Fact]
    public void should_load_only_the_commits_starting_from_the_checkpoint()
    {
        Assert.All(_committed.Skip(CheckPoint), x => Assert.True(_loaded.Contains(x)));
    }
}

public class WhenPagingOverAllCommitsOfABucketFromAParticularCheckpoint : PersistenceEngineConcern
{
    private List<Guid> _committedOnBucket1 = null!;
    private List<Guid> _committedOnBucket2 = null!;
    private ICollection<Guid> _loaded = null!;
    private const int CheckPoint = 2;

    protected override async Task Context()
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

    protected override async Task Because()
    {
        var enumerable = Persistence.GetFrom("b1", CheckPoint, CancellationToken.None);
        _loaded = (await enumerable.ToList().ConfigureAwait(false)).Select(c => c.CommitId).ToList();
    }

    [Fact]
    public void should_load_the_same_number_of_commits_which_have_been_persisted_starting_from_the_checkpoint()
    {
        Assert.Equal(_committedOnBucket1.Count - CheckPoint, _loaded.Count);
    }

    [Fact]
    public void should_load_only_the_commits_on_bucket1_starting_from_the_checkpoint()
    {
        Assert.All(_committedOnBucket1.Skip(CheckPoint), x => Assert.True(_loaded.Contains(x)));
    }

    [Fact]
    public void should_not_load_the_commits_from_bucket2()
    {
        Assert.All(_committedOnBucket2, x => Assert.False(_loaded.Contains(x)));
    }
}

public class WhenReadingAllCommitsFromTheYear1Ad : PersistenceEngineConcern
{
    private Exception _thrown = null!;

    protected override async Task Because()
    {
        // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
        _thrown = (await Catch.Exception(
                async () =>
                {
                    var enumerable = Persistence.GetFrom(DateTimeOffset.MinValue);
                    _ = await enumerable.FirstOrDefault(CancellationToken.None).ConfigureAwait(false);
                })
            .ConfigureAwait(false))!;
    }

    [Fact]
    public void should_NOT_throw_an_exception()
    {
        Assert.Null(_thrown);
    }
}

public class WhenPurgingAllCommits : PersistenceEngineConcern
{
    protected override Task Context() => Persistence.CommitSingle();

    protected override Task Because() => Persistence.Purge();

    [Fact]
    public async Task should_not_find_any_commits_stored()
    {
        var enumerable = Persistence.GetFrom(DateTimeOffset.MinValue);
        Assert.Empty(await enumerable.ToList(CancellationToken.None));
    }

    [Fact]
    public async Task should_not_find_any_streams_to_snapshot() =>
        Assert.Equal(0,
            await Persistence.GetStreamsToSnapshot(0, CancellationToken.None).Count());
}

public class WhenInvokingAfterDisposal : PersistenceEngineConcern
{
    private Exception _thrown = null!;

    protected override Task Context()
    {
        Persistence.Dispose();

        return Task.CompletedTask;
    }

    protected override async Task Because()
    {
        _thrown = (await Catch.Exception(() => Persistence.CommitSingle()).ConfigureAwait(false))!;
    }

    [Fact]
    public void should_throw_an_ObjectDisposedException()
    {
        Assert.IsType<ObjectDisposedException>(_thrown);
    }
}

public class WhenCommittingAStreamWithTheSameIdAsAStreamSameBucket : PersistenceEngineConcern
{
    private string _streamId = null!;
    private static Exception _thrown = null!;

    protected override async Task Context()
    {
        _streamId = Guid.NewGuid().ToString();
        await Persistence.Commit(_streamId.BuildAttempt()).ConfigureAwait(false);
    }

    protected override async Task Because()
    {
        _thrown = (await Catch.Exception(() => Persistence.Commit(_streamId.BuildAttempt())))!;
    }

    [Fact]
    public void should_throw()
    {
        Assert.NotNull(_thrown);
    }

    [Fact]
    public void should_be_duplicate_commit_exception()
    {
        Assert.IsType<ConcurrencyException>(_thrown);
    }
}

public class WhenCommittingAStreamWithTheSameIdAsAStreamInAnotherBucket : PersistenceEngineConcern
{
    private const string BucketAId = "a";
    private const string BucketBId = "b";
    private string _streamId = null!;
    private static IEventStream _attemptForBucketB = null!;
    private static Exception _thrown = null!;
    private DateTimeOffset _attemptACommitStamp;

    protected override async Task Context()
    {
        _streamId = Guid.NewGuid().ToString();
        var now = SystemTime.UtcNow;
        await Persistence.Commit(_streamId.BuildAttempt(BucketAId)).ConfigureAwait(false);
        var enumerable = Persistence.GetFrom(BucketAId, _streamId, 0, int.MaxValue, CancellationToken.None);
        _attemptACommitStamp =
            (await enumerable.First())
            .CommitStamp;
        _attemptForBucketB = _streamId.BuildAttempt(BucketBId);
    }

    protected override async Task Because()
    {
        _thrown = (await Catch.Exception(() => Persistence.Commit(_attemptForBucketB)).ConfigureAwait(false))!;
    }

    [Fact]
    public void should_succeed()
    {
        Assert.Null(_thrown);
    }

    [Fact]
    public async Task should_persist_to_the_correct_bucket()
    {
        var enumerable = Persistence.GetFrom(BucketBId, _streamId, 0, int.MaxValue, CancellationToken.None);
        var stream = await enumerable.ToList();
        Assert.NotNull(stream);
        Assert.Single(stream);
    }

    [Fact]
    public async Task should_not_affect_the_stream_from_the_other_bucket()
    {
        var enumerable = Persistence.GetFrom(BucketAId, _streamId, 0, int.MaxValue, CancellationToken.None);
        var stream = await enumerable.ToList();
        Assert.NotNull(stream);
        Assert.Single(stream);
        Assert.Equal(_attemptACommitStamp, stream.First().CommitStamp);
    }
}

public class WhenSavingASnapshotForAStreamWithTheSameIdAsAStreamInAnotherBucket : PersistenceEngineConcern
{
    private const string BucketAId = "a";
    private const string BucketBId = "b";
    private string _streamId = null!;
    private static Snapshot _snapshot = null!;

    protected override async Task Context()
    {
        _streamId = Guid.NewGuid().ToString();
        _snapshot = new Snapshot(BucketBId, _streamId, 1, "Snapshot");
        await Persistence.Commit(_streamId.BuildAttempt(bucketId: BucketAId)).ConfigureAwait(false);
        await Persistence.Commit(_streamId.BuildAttempt(bucketId: BucketBId)).ConfigureAwait(false);
    }

    protected override Task Because()
    {
        Persistence.AddSnapshot(_snapshot);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task should_affect_snapshots_from_another_bucket() =>
        Assert.Null(await Persistence
            .GetSnapshot(BucketAId, _streamId, _snapshot.StreamRevision, CancellationToken.None));
}

public class
    WhenReadingAllCommitsFromAParticularPointInTimeAndThereAreStreamsInMultipleBuckets : PersistenceEngineConcern
{
    private const string BucketAId = "a";
    private const string BucketBId = "b";

    private static DateTimeOffset _now;
    private static ICommit[] _returnedCommits = null!;
    private ICommit _commitToBucketB = null!;

    protected override async Task Context()
    {
        _now = SystemTime.UtcNow.AddYears(1);

        var commitToBucketA = Guid.NewGuid().ToString().BuildAttempt();

        await Persistence.Commit(commitToBucketA).ConfigureAwait(false);
        await Persistence.Commit(commitToBucketA = commitToBucketA.BuildNextAttempt()).ConfigureAwait(false);
        await Persistence.Commit(commitToBucketA = commitToBucketA.BuildNextAttempt()).ConfigureAwait(false);
        await Persistence.Commit(commitToBucketA.BuildNextAttempt()).ConfigureAwait(false);

        var stream = Guid.NewGuid().ToString().BuildAttempt();

        _commitToBucketB = (await Persistence.Commit(stream))!;
    }

    protected override async Task Because()
    {
        var enumerable = Persistence.GetFrom(BucketAId, _now);
        _returnedCommits = await enumerable.ToArray().ConfigureAwait(false);
    }

    [Fact]
    public void should_not_return_commits_from_other_buckets()
    {
        Assert.DoesNotContain(_returnedCommits, c => c.CommitId.Equals(_commitToBucketB.CommitId));
    }
}

public class WhenGettingAllCommitsSinceCheckpointAndThereAreStreamsInMultipleBuckets : PersistenceEngineConcern
{
    private ICommit[] _commits = null!;

    protected override async Task Context()
    {
        const string bucketAId = "a";
        const string bucketBId = "b";
        await Persistence.Commit(Guid.NewGuid().ToString().BuildAttempt(bucketId: bucketAId)).ConfigureAwait(false);
        await Persistence.Commit(Guid.NewGuid().ToString().BuildAttempt(bucketId: bucketBId)).ConfigureAwait(false);
        await Persistence.Commit(Guid.NewGuid().ToString().BuildAttempt(bucketId: bucketAId)).ConfigureAwait(false);
    }

    protected override async Task Because()
    {
        var enumerable = Persistence.GetFromStart("a");
        _commits = await enumerable.ToArray().ConfigureAwait(false);
    }

    [Fact]
    public void should_not_be_empty()
    {
        Assert.NotEmpty(_commits);
    }

    [Fact]
    public void should_be_in_order_by_checkpoint()
    {
        long checkpoint = 0;
        foreach (var commit in _commits)
        {
            var commitCheckpoint = commit.CheckpointToken;
            Assert.True(commitCheckpoint > checkpoint);
            checkpoint = commit.CheckpointToken;
        }
    }
}

public class WhenPurgingAllCommitsAndThereAreStreamsInMultipleBuckets : PersistenceEngineConcern
{
    private const string BucketAId = "a";
    private const string BucketBId = "b";
    private string _streamId = null!;

    protected override async Task Context()
    {
        _streamId = Guid.NewGuid().ToString();
        await Persistence.Commit(_streamId.BuildAttempt(bucketId: BucketAId)).ConfigureAwait(false);
        await Persistence.Commit(_streamId.BuildAttempt(bucketId: BucketBId)).ConfigureAwait(false);
    }

    protected override Task Because()
    {
        Persistence.Purge();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task should_purge_all_commits_stored_in_bucket_a()
    {
        var asyncEnumerable = Persistence.GetFrom(BucketAId, DateTimeOffset.MinValue);
        Assert.Equal(0, await asyncEnumerable.Count());
    }

    [Fact]
    public async Task should_purge_all_commits_stored_in_bucket_b()
    {
        var enumerable = Persistence.GetFrom(BucketBId, DateTimeOffset.MinValue);
        Assert.Equal(0, await enumerable.Count());
    }

    [Fact]
    public async Task should_purge_all_streams_to_snapshot_in_bucket_a() => Assert.Equal(0, await Persistence
        .GetStreamsToSnapshot(BucketAId, 0, CancellationToken.None).Count());

    [Fact]
    public async Task should_purge_all_streams_to_snapshot_in_bucket_b() => Assert.Equal(0, await Persistence
        .GetStreamsToSnapshot(BucketBId, 0, CancellationToken.None).Count());
}

public class WhenGettingFromCheckpointAmountOfCommitsExceedsPageSize : PersistenceEngineConcern
{
    private ICommit[] _commits = null!;
    private int _moreThanPageSize;

    protected override async Task Because()
    {
        _moreThanPageSize = ConfiguredPageSizeForTesting + 1;
        var eventStore =
            new OptimisticEventStore(Persistence, Enumerable.Empty<IPipelineHook>(), NullLoggerFactory.Instance);
        // TODO: Not sure how to set the actual page size to the const defined above
        for (var i = 0; i < _moreThanPageSize; i++)
        {
            var stream = await eventStore.OpenStream(Guid.NewGuid());
            stream.Add(new EventMessage(new Pippo { S = "Hi " + i }));
            await Persistence.Commit(stream);
        }

        var enumerable = Persistence.GetFrom(DateTimeOffset.MinValue);
        _ = await enumerable.ToArray().ConfigureAwait(false);
        var asyncEnumerable = Persistence.GetFrom(Bucket.Default, 0, CancellationToken.None);
        _commits = await asyncEnumerable.ToArray().ConfigureAwait(false);
    }

    [Fact]
    public void Should_have_expected_number_of_commits()
    {
        Assert.Equal(_moreThanPageSize, _commits.Length);
    }
}

public class WhenAPayloadIsLarge : PersistenceEngineConcern
{
    [Fact]
    public async Task can_commit()
    {
        const int bodyLength = 100000;
        var streamId = Guid.NewGuid().ToString();
        var stream = OptimisticEventStream.Create(Bucket.Default, streamId);
        stream.Add(new EventMessage(new string('a', bodyLength)));
        await Persistence.Commit(stream);

        var commits = await Persistence.GetFrom(streamId, 0, int.MaxValue, CancellationToken.None).Single();
        Assert.Equal(bodyLength, commits.Events.Single().Body.ToString()!.Length);
    }
}

/// <summary>
/// We are adapting the tests to use 3 different frameworks:
/// - XUnit: the attached test runner does the job (fixture setup and cleanup)
/// - NUnit (.net core project)
/// - MSTest (.net core project)
/// </summary>
public abstract class PersistenceEngineConcern : SpecificationBase, IClassFixture<PersistenceEngineFixture>
{
    private PersistenceEngineFixture _fixture = null!;

    public PersistenceEngineConcern()
    {
        SetFixture();
        OnStart().GetAwaiter().GetResult();
    }

    protected IPersistStreams Persistence => _fixture.Persistence;

    protected static int ConfiguredPageSizeForTesting => 2;

    public void SetFixture()
    {
        _fixture = new PersistenceEngineFixture();
        _fixture.Initialize(ConfiguredPageSizeForTesting);
    }

    protected override void Cleanup()
    {
        base.Cleanup();
        _fixture.Dispose();
    }
}

[Serializable]
public class Pippo
{
    public string S { get; set; } = null!;
}
