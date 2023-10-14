namespace OpenMedStack.NEventStore.Tests;

using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using NEventStore.Persistence.AcceptanceTests.BDD;
using Xunit;

public class WhenBuildingAStream : OnTheEventStream
{
    private const int MinRevision = 2;
    private const int MaxRevision = 7;
    private readonly int _eachCommitHas = 2.Events();
    private ICommit[] _committed = null!;

    protected override Task Context()
    {
        _committed = new[]
        {
            BuildCommitStub(2, 1, _eachCommitHas), // 1-2
            BuildCommitStub(4, 2, _eachCommitHas), // 3-4
            BuildCommitStub(6, 3, _eachCommitHas), // 5-6
            BuildCommitStub(8, 3, _eachCommitHas) // 7-8
        };

        _committed[0].Headers["Common"] = string.Empty;
        _committed[1].Headers["Common"] = string.Empty;
        _committed[2].Headers["Common"] = string.Empty;
        _committed[3].Headers["Common"] = string.Empty;
        _committed[0].Headers["Unique"] = string.Empty;

        A.CallTo(() => Persistence.GetFrom(BucketId, StreamId, MinRevision, MaxRevision, default))
            .Returns(_committed.ToAsyncEnumerable());

        return Task.CompletedTask;
    }

    protected override async Task Because()
    {
        Stream = await OptimisticEventStream
            .Create(BucketId, StreamId, Persistence, MinRevision, MaxRevision,
                NullLogger<OptimisticEventStream>.Instance)
            .ConfigureAwait(false);
    }

    [Fact]
    public void should_have_the_correct_stream_identifier()
    {
        Assert.Equal(StreamId, Stream.StreamId);
    }

    [Fact]
    public void should_have_the_correct_head_stream_revision()
    {
        Assert.Equal(MaxRevision, Stream.StreamRevision);
    }

    [Fact]
    public void should_have_the_correct_head_commit_sequence()
    {
        Assert.Equal(_committed.Last().CommitSequence, Stream.CommitSequence);
    }

    [Fact]
    public void should_not_include_events_below_the_minimum_revision_indicated()
    {
        Assert.Equal(_committed.First().Events.Last(), Stream.CommittedEvents.First());
    }

    [Fact]
    public void should_not_include_events_above_the_maximum_revision_indicated()
    {
        Assert.NotStrictEqual(_committed.First().Events.Last(), Stream.CommittedEvents.Last());
    }

    [Fact]
    public void should_have_all_of_the_committed_events_up_to_the_stream_revision_specified()
    {
        Assert.Equal(MaxRevision - MinRevision + 1, Stream.CommittedEvents.Count);
    }

    [Fact]
    public void should_contain_the_headers_from_the_underlying_commits()
    {
        Assert.Equal(2, Stream.CommittedHeaders.Count);
    }
}

public class WhenTheHeadEventRevisionIsLessThanTheMaxDesiredRevision : OnTheEventStream
{
    private readonly int _eventsPerCommit = 2.Events();
    private ICommit[] _committed = null!;

    protected override Task Context()
    {
        _committed = new[]
        {
            BuildCommitStub(2, 1, _eventsPerCommit), // 1-2
            BuildCommitStub(4, 2, _eventsPerCommit), // 3-4
            BuildCommitStub(6, 3, _eventsPerCommit), // 5-6
            BuildCommitStub(8, 3, _eventsPerCommit) // 7-8
        };

        A.CallTo(() => Persistence.GetFrom(BucketId, StreamId, 0, int.MaxValue, default))
            .Returns(_committed.ToAsyncEnumerable());

        return Task.CompletedTask;
    }

    protected override async Task Because()
    {
        Stream = await OptimisticEventStream.Create(BucketId, StreamId, Persistence, 0, int.MaxValue,
                NullLogger<OptimisticEventStream>.Instance)
            .ConfigureAwait(false);
    }

    [Fact]
    public void should_set_the_stream_revision_to_the_revision_of_the_most_recent_event()
    {
        Assert.Equal(_committed.Last().StreamRevision, Stream.StreamRevision);
    }
}

public class WhenAddingAFullyPopulatedEventMessage : OnTheEventStream
{
    protected override Task Because()
    {
        Stream.Add(new EventMessage("populated"));

        return Task.CompletedTask;
    }

    [Fact]
    public void should_add_the_event_to_the_set_of_uncommitted_events()
    {
        Assert.Single(Stream.UncommittedEvents);
    }
}

public class WhenAddingMultiplePopulatedEventMessages : OnTheEventStream
{
    protected override Task Because()
    {
        Stream.Add(new EventMessage("populated"));
        Stream.Add(new EventMessage("also populated"));

        return Task.CompletedTask;
    }

    [Fact]
    public void should_add_all_of_the_events_provided_to_the_set_of_uncommitted_events()
    {
        Assert.Equal(2, Stream.UncommittedEvents.Count);
    }
}

public class WhenAddingASimpleObjectAsAnEventMessage : OnTheEventStream
{
    private const string MyEvent = "some event data";

    protected override Task Because()
    {
        Stream.Add(new EventMessage(MyEvent));

        return Task.CompletedTask;
    }

    [Fact]
    public void should_add_the_uncommitted_event_to_the_set_of_uncommitted_events()
    {
        Assert.Single(Stream.UncommittedEvents);
    }

    [Fact]
    public void should_wrap_the_uncommitted_event_in_an_EventMessage_object()
    {
        Assert.Equal(MyEvent, Stream.UncommittedEvents.First().Body);
    }
}

public class WhenClearingAnyUncommittedChanges : OnTheEventStream
{
    protected override Task Context()
    {
        Stream.Add(new EventMessage(string.Empty));

        return Task.CompletedTask;
    }

    protected override Task Because()
    {
        Stream.ClearChanges();

        return Task.CompletedTask;
    }

    [Fact]
    public void should_clear_all_uncommitted_events()
    {
        Assert.Empty(Stream.UncommittedEvents);
    }
}

public class WhenCommittingAnEmptyChangeset : OnTheEventStream
{
    protected override Task Because() => Persistence.Commit(Stream, Guid.NewGuid());

    [Fact]
    public void should_not_increment_the_current_stream_revision()
    {
        Assert.Equal(0, Stream.StreamRevision);
    }

    [Fact]
    public void should_not_increment_the_current_commit_sequence()
    {
        Assert.Equal(0, Stream.CommitSequence);
    }
}

public class WhenCommittingAnyUncommittedChanges : OnTheEventStream
{
    private readonly Guid _commitId = Guid.NewGuid();
    private readonly Dictionary<string, object> _headers = new() { { "key", "value" } };
    private readonly EventMessage _uncommitted = new(string.Empty);
    private CommitAttempt _constructed = null!;

    protected override Task Context()
    {
        A.CallTo(() => Persistence.Commit(A<IEventStream>._, A<Guid?>._, A<CancellationToken>._))
            .Invokes((IEventStream e, Guid? g, CancellationToken _) =>
            {
                if (e.UncommittedEvents.Count <= 0)
                {
                    return;
                }

                _constructed = new CommitAttempt(
                    e.BucketId,
                    e.StreamId,
                    e.StreamRevision,
                    g!.Value,
                    e.CommitSequence + 1,
                    SystemTime.UtcNow,
                    e.UncommittedHeaders.ToDictionary(),
                    e.UncommittedEvents.ToList());
                e.SetPersisted(e.CommitSequence + 1);
            })
            .ReturnsLazily(
                (IEventStream _, Guid? _, CancellationToken _) => new Commit(
                    _constructed.BucketId,
                    _constructed.StreamId,
                    _constructed.StreamRevision,
                    _constructed.CommitId,
                    _constructed.CommitSequence,
                    _constructed.CommitStamp,
                    0,
                    _constructed.Headers,
                    _constructed.Events));
        Stream.Add(_uncommitted);
        foreach (var item in _headers)
        {
            Stream.Add(item.Key, item.Value);
        }

        return Task.CompletedTask;
    }

    protected override Task Because() => Persistence.Commit(Stream, _commitId);

    [Fact]
    public void should_provide_a_commit_to_the_underlying_infrastructure()
    {
        A.CallTo(() => Persistence.Commit(A<IEventStream>._, A<Guid?>._, A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
    }

    [Fact]
    public void should_build_the_commit_with_the_correct_bucket_identifier()
    {
        Assert.Equal(BucketId, _constructed.BucketId);
    }

    [Fact]
    public void should_build_the_commit_with_the_correct_stream_identifier()
    {
        Assert.Equal(StreamId, _constructed.StreamId);
    }

    [Fact]
    public void should_build_the_commit_with_the_correct_stream_revision()
    {
        Assert.Equal(DefaultStreamRevision, _constructed.StreamRevision);
    }

    [Fact]
    public void should_build_the_commit_with_the_correct_commit_identifier()
    {
        Assert.Equal(_commitId, _constructed.CommitId);
    }

    [Fact]
    public void should_build_the_commit_with_an_incremented_commit_sequence()
    {
        Assert.Equal(DefaultCommitSequence, _constructed.CommitSequence);
    }

    [Fact]
    public void should_build_the_commit_with_the_correct_commit_stamp()
    {
        Assert.Equal(_constructed.CommitStamp.DateTime, SystemTime.UtcNow.DateTime, TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void should_build_the_commit_with_the_headers_provided()
    {
        Assert.Equal(_headers.First().Value, _constructed.Headers[_headers.First().Key]);
    }

    [Fact]
    public void should_build_the_commit_containing_all_uncommitted_events()
    {
        Assert.Equal(_headers.Count, _constructed.Events.Count);
    }

    [Fact]
    public void should_build_the_commit_using_the_event_messages_provided()
    {
        Assert.Equal(_uncommitted, _constructed.Events.First());
    }

    [Fact]
    public void should_contain_a_copy_of_the_headers_provided()
    {
        Assert.NotEmpty(_constructed.Headers);
    }

    [Fact]
    public void should_update_the_stream_revision()
    {
        Assert.Equal(_constructed.StreamRevision, Stream.StreamRevision);
    }

    [Fact]
    public void should_update_the_commit_sequence()
    {
        Assert.Equal(_constructed.CommitSequence, Stream.CommitSequence);
    }

    [Fact]
    public void should_add_the_uncommitted_events_the_committed_events()
    {
        Assert.Equal(_uncommitted, Stream.CommittedEvents.Last());
    }

    [Fact]
    public void should_clear_the_uncommitted_events_on_the_stream()
    {
        Assert.Empty(Stream.UncommittedEvents);
    }

    [Fact]
    public void should_clear_the_uncommitted_headers_on_the_stream()
    {
        Assert.Empty(Stream.UncommittedHeaders);
    }

    [Fact]
    public void should_copy_the_uncommitted_headers_to_the_committed_stream_headers()
    {
        Assert.Equal(_headers.Count, Stream.CommittedHeaders.Count);
    }
}

public abstract class OnTheEventStream : SpecificationBase, IClassFixture<FakeTimeFixture>
{
    protected const int DefaultStreamRevision = 1;
    protected const int DefaultCommitSequence = 1;
    private ICommitEvents? _persistence;
    private OptimisticEventStream? _stream;
    protected const string BucketId = "bucket";
    protected readonly string StreamId = Guid.NewGuid().ToString();

    public OnTheEventStream()
    {
        OnStart().Wait();
    }

    protected ICommitEvents Persistence => _persistence ??= A.Fake<ICommitEvents>();

    protected OptimisticEventStream Stream
    {
        get => _stream ??= OptimisticEventStream
            .Create(BucketId, StreamId, NullLogger<OptimisticEventStream>.Instance);
        set => _stream = value;
    }

    protected ICommit BuildCommitStub(int revision, int sequence, int eventCount)
    {
        var events = new List<EventMessage>(eventCount);
        for (var i = 0; i < eventCount; i++)
        {
            events.Add(new EventMessage(string.Empty));
        }

        return new Commit(
            Bucket.Default,
            StreamId,
            revision,
            Guid.NewGuid(),
            sequence,
            SystemTime.UtcNow,
            0,
            null,
            events);
    }
}

public class FakeTimeFixture : IDisposable
{
    public FakeTimeFixture()
    {
        SystemTime.Resolver = () => new DateTime(2012, 1, 1, 13, 0, 0);
    }

    public void Dispose()
    {
        SystemTime.Resolver = () => DateTimeOffset.MinValue;
        GC.SuppressFinalize(this);
    }
}
