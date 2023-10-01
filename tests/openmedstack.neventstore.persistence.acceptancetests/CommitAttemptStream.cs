using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests;

public class CommitAttemptStream : IEventStream
{
    private readonly CommitAttempt _attempt;

    public CommitAttemptStream(CommitAttempt attempt)
    {
        _attempt = attempt;
    }

    public string BucketId
    {
        get { return _attempt.BucketId; }
    }

    public string StreamId
    {
        get { return _attempt.StreamId; }
    }

    public int StreamRevision
    {
        get { return _attempt.StreamRevision; }
    }

    public int CommitSequence
    {
        get { return _attempt.CommitSequence; }
    }

    public IReadOnlyCollection<EventMessage> CommittedEvents { get; } = new List<EventMessage>();
    public IDictionary<string, object> CommittedHeaders { get; } = new Dictionary<string, object>();

    public IReadOnlyCollection<EventMessage> UncommittedEvents
    {
        get { return _attempt.Events.ToList(); }
    }

    public IDictionary<string, object> UncommittedHeaders
    {
        get { return _attempt.Headers; }
    }

    public void Add(EventMessage uncommittedEvent)
    {
    }

    public void SetPersisted(int commitSequence)
    {
    }

    public Task Update(ICommitEvents commitEvents, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
