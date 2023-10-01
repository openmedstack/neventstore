using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using OpenMedStack.NEventStore.Grpc;

namespace OpenMedStack.NEventStore.Server.Service;

public class EventStoreService : EventStore.EventStoreBase
{
    private readonly IPersistStreams _persistence;
    private readonly ISerialize _serializer;

    public EventStoreService(IPersistStreams persistence, ISerialize serializer)
    {
        _persistence = persistence;
        _serializer = serializer;
    }

    public override async Task<CommitInfo?> Commit(CommitInfo request, ServerCallContext context)
    {
        static EventMessage ToEventMessage(EventMessageInfo info)
        {
            return new EventMessage(info.Base64Payload, info.Headers.ToDictionary(x => x.Key, x => (object)x.Value));
        }

        var stream = await OptimisticEventStream.Create(request.BucketId, request.StreamId, _persistence, 0,
            request.StreamRevision,
            NullLogger<OptimisticEventStream>.Instance, context.CancellationToken).ConfigureAwait(false);
        foreach (var requestEvent in request.Events)
        {
            stream.Add(ToEventMessage(requestEvent));
        }

        foreach (var header in request.Headers)
        {
            stream.UncommittedHeaders[header.Key] = header.Value;
        }

        var info = await _persistence.Commit(stream, Guid.Parse(request.CommitId), context.CancellationToken)
            .ConfigureAwait(false);
        return info == null
            ? null
            : new CommitInfo
            {
                BucketId = info.BucketId, CommitId = info.CommitId.ToString("N"),
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
        var stream = _persistence.GetFrom(request.BucketId, request.StreamId, request.MinRevision, request.MaxRevision,
            context.CancellationToken);

        await WriteToStream(responseStream, context, stream).ConfigureAwait(false);
    }

    public override async Task<BoolValue> AddSnapshot(SnapshotInfo request, ServerCallContext context)
    {
        var result = await _persistence.AddSnapshot(
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
        var result = await _persistence.GetSnapshot(request.BucketId, request.StreamId, request.MaxRevision,
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

    public override async Task GetFromTimestamp(
        GetFromTimestampRequest request,
        IServerStreamWriter<CommitInfo> responseStream,
        ServerCallContext context)
    {
        var commits = _persistence.GetFrom(request.BucketId,
            DateTimeOffset.FromUnixTimeSeconds(request.Timestamp), context.CancellationToken);

        await WriteToStream(responseStream, context, commits).ConfigureAwait(false);
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

    public override async Task GetFromTo(
        GetFromToTimestampsRequest request,
        IServerStreamWriter<CommitInfo> responseStream,
        ServerCallContext context)
    {
        var commits = _persistence.GetFromTo(request.BucketId,
            DateTimeOffset.FromUnixTimeSeconds(request.From), DateTimeOffset.FromUnixTimeSeconds(request.To),
            context.CancellationToken);
        await WriteToStream(responseStream, context, commits).ConfigureAwait(false);
    }

    public override async Task GetFromCheckpointToken(
        GetFromCheckpointTokenRequest request,
        IServerStreamWriter<CommitInfo> responseStream,
        ServerCallContext context)
    {
        var commits = _persistence.GetFrom(request.BucketId,
            request.CheckpointToken,
            context.CancellationToken);
        await WriteToStream(responseStream, context, commits).ConfigureAwait(false);
    }

    public override async Task GetStreamsToSnapshot(
        StreamToSnapshotRequest request,
        IServerStreamWriter<StreamHeadInfo> responseStream,
        ServerCallContext context)
    {
        var heads = _persistence.GetStreamsToSnapshot(request.BucketId, request.MaxRevision, context.CancellationToken);
        await foreach (var head in heads.ConfigureAwait(false))
        {
            await responseStream.WriteAsync(new StreamHeadInfo
            {
                BucketId = head.BucketId,
                StreamId = head.StreamId,
                HeadRevision = head.HeadRevision,
                SnapshotRevision = head.SnapshotRevision
            }, context.CancellationToken).ConfigureAwait(false);
        }
    }

    public override async Task<BoolValue> Purge(PurgeRequest request, ServerCallContext context)
    {
        var result = await _persistence.Purge(request.BucketId).ConfigureAwait(false);
        return new BoolValue { Value = result };
    }

    public override async Task<BoolValue> DeleteStream(DeleteStreamRequest request, ServerCallContext context)
    {
        var result = await _persistence.DeleteStream(request.BucketId, request.StreamId).ConfigureAwait(false);
        return new BoolValue { Value = result };
    }
}
