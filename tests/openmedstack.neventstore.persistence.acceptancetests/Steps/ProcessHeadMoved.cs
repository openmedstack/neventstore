using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

public partial class PersistenceEngineBehavior
{
    private const string BucketId = "bucket";
    private readonly string StreamId = Guid.NewGuid().ToString();
    private const int StreamRevision = 1;
    private readonly EventMessage _uncommitted = new(string.Empty);
    private ICommit[] _committed = null!;
    private ICommit? _commit;
    private IEventStream _stream = null!;

    [Given("a persisted stream with a single event")]
    public async Task APersistedStreamWithASingleEvent()
    {
        _committed = [BuildCommitStub(1, 1, 1)];
        var stream = OptimisticEventStream.Create(BucketId, StreamId, NullLogger<OptimisticEventStream>.Instance);
        stream.Add(_committed[0].Events.First());
        await Persistence.Commit(stream, Guid.NewGuid());
    }

    [When("committing after another thread or process has moved the stream head")]
    public async Task WhenCommittingAfterAnotherThreadOrProcessHasMovedTheStreamHead()
    {
        _stream = await OptimisticEventStream
            .Create(BucketId, StreamId, Persistence, StreamRevision, int.MaxValue,
                NullLogger<OptimisticEventStream>.Instance);
        var competingStream = await OptimisticEventStream.Create(BucketId, StreamId, Persistence, StreamRevision,
            int.MaxValue,
            NullLogger<OptimisticEventStream>.Instance);
        foreach (var c in BuildCommitStub(3, 2, 2).Events)
        {
            competingStream.Add(c);
        }

        await Persistence.Commit(competingStream, Guid.NewGuid());

        _stream.Add(_uncommitted);
        _commit = await Persistence.Commit(_stream, Guid.NewGuid());
    }

    [Then("should update the stream revision accordingly")]
    public void ShouldUpdateTheStreamRevisionAccordingly()
    {
        Assert.Equal(3, _commit?.CommitSequence);
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
