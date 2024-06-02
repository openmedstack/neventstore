using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using OpenMedStack.NEventStore.Grpc;

namespace OpenMedStack.NEventStore.Server.Service;

public class EventStoreService : EventStore.EventStoreBase
{
    private readonly ICommitEvents _persistence;
    private readonly IAccessSnapshots _snapshots;
    private readonly ISerialize _serializer;

    public EventStoreService(ICommitEvents persistence, IAccessSnapshots snapshots, ISerialize serializer)
    {
        _persistence = persistence;
        _snapshots = snapshots;
        _serializer = serializer;
    }

    public override async Task<CommitInfo?> Commit(CommitInfo request, ServerCallContext context)
    {
        static EventMessage ToEventMessage(EventMessageInfo info)
        {
            return new EventMessage(info.Base64Payload, info.Headers.ToDictionary(x => x.Key, x => (object)x.Value));
        }

        var commitAttempt = new CommitAttempt(request.BucketId, request.StreamId, request.StreamRevision,
            Guid.Parse(request.CommitId), request.CommitSequence,
            DateTimeOffset.FromUnixTimeSeconds(request.CommitStamp),
            request.Headers.ToDictionary(x => x.Key, x => (object)x.Value),
            request.Events.Select(ToEventMessage).ToList());

        var info = await _persistence.Commit(commitAttempt, context.CancellationToken)
            .ConfigureAwait(false);
        return info == null
            ? null
            : new CommitInfo
            {
                BucketId = info.BucketId,
                CommitId = info.CommitId.ToString("N"),
                CheckpointToken = info.CheckpointToken,
                CommitSequence = info.CommitSequence, CommitStamp = info.CommitStamp.ToUnixTimeSeconds(),
                StreamId = info.StreamId, StreamRevision = info.StreamRevision,
                Events = { info.Events.Select(e => e.ToEventMessageInfo()) }
            };
    }

    public override async Task GetFromMinMax(
        GetFromMinMaxRequest request,
        IServerStreamWriter<CommitInfo> responseStream,
        ServerCallContext context)
    {
        var stream = _persistence.Get(request.BucketId, request.StreamId, request.MinRevision, request.MaxRevision,
            context.CancellationToken);

        await WriteToStream(responseStream, context, stream).ConfigureAwait(false);
    }

    public override async Task<BoolValue> AddSnapshot(SnapshotInfo request, ServerCallContext context)
    {
        var result = await _snapshots.AddSnapshot(
                new Snapshot(
                    request.BucketId,
                    request.StreamId,
                    request.StreamRevision,
                    _serializer.Deserialize<object>(Convert.FromBase64String(request.Base64Payload))!))
            .ConfigureAwait(false);
        return new BoolValue { Value = result };
    }

    public override async Task<SnapshotInfo?> GetSnapshot(GetSnapshotRequest request, ServerCallContext context)
    {
        var result = await _snapshots.GetSnapshot(request.BucketId, request.StreamId, request.MaxRevision,
            context.CancellationToken).ConfigureAwait(false);
        if (result == null)
        {
            return null;
        }

        using var stream = new MemoryStream();
        _serializer.Serialize(stream, result.Payload);
        var bytes = Convert.ToBase64String(stream.ToArray());
        return new SnapshotInfo
        {
            BucketId = result.BucketId,
            StreamId = result.StreamId,
            StreamRevision = result.StreamRevision,
            Base64Payload = bytes
        };
    }

    private static async Task WriteToStream(
        IServerStreamWriter<CommitInfo> responseStream,
        ServerCallContext context,
        IAsyncEnumerable<ICommit> commits)
    {
        await foreach (var commit in commits.ConfigureAwait(false))
        {
            await responseStream.WriteAsync(new CommitInfo
            {
                BucketId = commit.BucketId,
                CommitId = commit.CommitId.ToString("N"),
                CommitSequence = commit.CommitSequence,
                CommitStamp = commit.CommitStamp.ToUnixTimeSeconds(),
                StreamId = commit.StreamId,
                StreamRevision = commit.StreamRevision,
                CheckpointToken = commit.CheckpointToken,
                Events = { commit.Events.Select(e => e.ToEventMessageInfo()) }
            }, context.CancellationToken).ConfigureAwait(false);
        }
    }
}
