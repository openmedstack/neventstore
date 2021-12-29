using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OpenMedStack.NEventStore.Persistence
{
    public class Commit : ICommit
    {
        public Commit(
            string bucketId,
            string streamId,
            int streamRevision,
            Guid commitId,
            int commitSequence,
            DateTimeOffset commitStamp,
            long checkpointToken,
            IDictionary<string, object>? headers,
            IEnumerable<EventMessage>? events)
        {
            BucketId = bucketId;
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

        public string BucketId { get; }

        public string StreamId { get; }

        public int StreamRevision { get; }

        public Guid CommitId { get; }

        public int CommitSequence { get; }

        public DateTimeOffset CommitStamp { get; }

        public IDictionary<string, object> Headers { get; }

        public ICollection<EventMessage> Events { get; }

        public long CheckpointToken { get; }
    }
}