using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests;

public class CommittedStream : IEventStream
{
    private readonly ICommit _commit;

    public CommittedStream(ICommit commit)
    {
        _commit = commit;
    }

    public string BucketId
    {
        get { return _commit.BucketId; }
    }

    public string StreamId
    {
        get { return _commit.StreamId; }
    }

    public int StreamRevision
    {
        get { return _commit.StreamRevision; }
    }

    public int CommitSequence
    {
        get { return _commit.CommitSequence; }
    }

    public IReadOnlyCollection<EventMessage> CommittedEvents
    {
        get { return _commit.Events.ToList(); }
    }

    public IDictionary<string, object> CommittedHeaders
    {
        get { return _commit.Headers; }
    }

    private List<EventMessage> _uncommittedEvents = new List<EventMessage>();

    public IReadOnlyCollection<EventMessage> UncommittedEvents
    {
        get { return _uncommittedEvents; }
    }

    public IDictionary<string, object> UncommittedHeaders { get; } = new Dictionary<string, object>();

    public void Add(EventMessage uncommittedEvent)
    {
        _uncommittedEvents.Add(uncommittedEvent);
    }

    public void SetPersisted(int commitSequence)
    {
    }

    public Task Update(ICommitEvents commitEvents, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
