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
    public IReadOnlyDictionary<string, object> CommittedHeaders { get; } = new Dictionary<string, object>();

    public IReadOnlyCollection<EventMessage> UncommittedEvents
    {
        get { return _attempt.Events.ToList(); }
    }

    public IReadOnlyDictionary<string, object> UncommittedHeaders
    {
        get { return _attempt.Headers.ToDictionary(); }
    }

    public void Add(EventMessage uncommittedEvent)
    {
    }

    public void Add(string key, object value)
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
