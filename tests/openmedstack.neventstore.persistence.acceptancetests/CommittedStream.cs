using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests;

public class CommittedStream : IEventStream
{
    private readonly List<EventMessage> _uncommittedEvents = new();
    private readonly ICommit _commit;
    private int _streamRevision;

    public CommittedStream(ICommit commit)
    {
        _commit = commit;
        _streamRevision = commit.StreamRevision;
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
        get { return _streamRevision; }
    }

    public int CommitSequence
    {
        get { return _commit.CommitSequence; }
    }

    public IReadOnlyCollection<EventMessage> CommittedEvents
    {
        get { return _commit.Events.ToList(); }
    }

    public IReadOnlyDictionary<string, object> CommittedHeaders
    {
        get { return _commit.Headers.ToDictionary(); }
    }

    public IReadOnlyCollection<EventMessage> UncommittedEvents
    {
        get { return _uncommittedEvents; }
    }

    public IReadOnlyDictionary<string, object> UncommittedHeaders { get; } = new Dictionary<string, object>();

    public void Add(EventMessage uncommittedEvent)
    {
        _uncommittedEvents.Add(uncommittedEvent);
        _streamRevision++;
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
