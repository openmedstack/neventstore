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
    private CommitAttempt _stream = null!;

    [Given("a persisted stream with a single event")]
    public async Task APersistedStreamWithASingleEvent()
    {
        _committed = [BuildCommitStub(1, 1, 1)];
        var stream = new CommitAttempt(BucketId,
            StreamId, StreamRevision, Guid.NewGuid(), 1, SystemTime.UtcNow,
            null,
            _committed[0].Events.ToArray());
        await Persistence.Commit(stream);
    }

    [When("committing after another thread or process has moved the stream head")]
    public async Task WhenCommittingAfterAnotherThreadOrProcessHasMovedTheStreamHead()
    {
        _stream = new CommitAttempt(BucketId, StreamId, 2, Guid.NewGuid(), 2, SystemTime.UtcNow, null,
            [new EventMessage(_uncommitted)]);
        var competingStream = new CommitAttempt(BucketId, StreamId, 3,
            Guid.NewGuid(), 2, SystemTime.UtcNow, null, BuildCommitStub(3, 2, 2).Events.ToArray());

        _ = await Persistence.Commit(competingStream);
    }

    [Then("must throw a concurrency exception")]
    public async Task MustThrowAConcurrencyException()
    {
        await Assert.ThrowsAsync<ConcurrencyException>(async () => await Persistence.Commit(_stream));
    }

    protected ICommit BuildCommitStub(int revision, int sequence, int eventCount)
    {
        var events = new List<EventMessage>(eventCount);
        for (var i = 0; i < eventCount; i++)
        {
            events.Add(new EventMessage(string.Empty));
        }

        return new Commit(
            "default",
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
