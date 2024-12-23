using System.Collections.ObjectModel;

namespace OpenMedStack.NEventStore.Abstractions.Persistence;

public class Commit : ICommit
{
    public Commit(
        string tenantId,
        string streamId,
        int streamRevision,
        Guid commitId,
        int commitSequence,
        DateTimeOffset commitStamp,
        long checkpointToken,
        IDictionary<string, object>? headers,
        IEnumerable<EventMessage>? events)
    {
        TenantId = tenantId;
        StreamId = streamId;
        StreamRevision = streamRevision;
        CommitId = commitId;
        CommitSequence = commitSequence;
        CommitStamp = commitStamp;
        CheckpointToken = checkpointToken;
        Headers = headers ?? new Dictionary<string, object>();
        Events = events == null ?
            new ReadOnlyCollection<EventMessage>(new List<EventMessage>()) :
            new ReadOnlyCollection<EventMessage>(new List<EventMessage>(events));
    }

    public string TenantId { get; }

    public string StreamId { get; }

    public int StreamRevision { get; }

    public Guid CommitId { get; }

    public int CommitSequence { get; }

    public DateTimeOffset CommitStamp { get; }

    public IDictionary<string, object> Headers { get; }

    public ICollection<EventMessage> Events { get; }

    public long CheckpointToken { get; }
}
