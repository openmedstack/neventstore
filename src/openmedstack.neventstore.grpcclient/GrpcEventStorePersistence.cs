using Grpc.Core;
using Grpc.Net.Client;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using OpenMedStack.NEventStore.Grpc;

namespace OpenMedStack.NEventStore.GrpcClient;

internal class GrpcEventStorePersistence : ICommitEvents, IAccessSnapshots
{
    private readonly ISerialize _serializer;
    private readonly EventStore.EventStoreClient _client;

    public GrpcEventStorePersistence(Uri endpoint, ISerialize serializer, GrpcChannelOptions? options = null)
    {
        _serializer = serializer;
        var channel = GrpcChannel.ForAddress(endpoint, options ?? new GrpcChannelOptions());
        _client = new EventStore.EventStoreClient(channel);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    public async Task<ICommit?> Commit(IEventStream eventStream, Guid? commitId, CancellationToken cancellationToken)
    {
        if (eventStream.UncommittedEvents.Count == 0)
        {
            return null;
        }

        commitId ??= Guid.NewGuid();
        var commitInfo = new CommitInfo
        {
            BucketId = eventStream.BucketId,
            StreamId = eventStream.StreamId,
            StreamRevision = eventStream.StreamRevision,
            CheckpointToken = 0,
            CommitId = commitId.Value.ToString("N"),
            CommitSequence = eventStream.CommitSequence + 1,
            CommitStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Events = { eventStream.UncommittedEvents.Select(Serialize) },
            Headers = { eventStream.UncommittedHeaders.ToDictionary(x => x.Key, x => Serialize(x.Value)) }
        };
        var commitResponse = await _client.CommitAsync(commitInfo).ConfigureAwait(false);

        return new Commit(commitResponse.BucketId, commitResponse.StreamId, commitResponse.StreamRevision,
            Guid.Parse(commitResponse.CommitId), commitResponse.CommitSequence,
            DateTimeOffset.FromUnixTimeSeconds(commitResponse.CommitStamp), commitResponse.CheckpointToken,
            commitResponse.Headers.ToDictionary(x => x.Key,
                x => _serializer.Deserialize<object>(Convert.FromBase64String(x.Value))!),
            commitResponse.Events.Select(ToEventMessage));
    }

    private EventMessage ToEventMessage(EventMessageInfo x)
    {
        return new EventMessage(_serializer.Deserialize<object>(Convert.FromBase64String(x.Base64Payload))!,
            x.Headers.ToDictionary(y => y.Key,
                y => _serializer.Deserialize<object>(Convert.FromBase64String(y.Value))!));
    }

    /// <inheritdoc />
    public async Task<ISnapshot?> GetSnapshot(
        string bucketId,
        string streamId,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        var response = await _client.GetSnapshotAsync(
            new GetSnapshotRequest { BucketId = bucketId, StreamId = streamId, MaxRevision = maxRevision },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return new Snapshot(response.BucketId, response.StreamId, response.StreamRevision,
            _serializer.Deserialize<object>(Convert.FromBase64String(response.Base64Payload))!);
    }

    /// <inheritdoc />
    public async Task<bool> AddSnapshot(ISnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var response = await _client.AddSnapshotAsync(null, cancellationToken: cancellationToken);
        return response.Value;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ICommit> Get(
        string bucketId,
        string streamId,
        int minRevision,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        var response = _client.GetFromMinMax(
            new GetFromMinMaxRequest
                { BucketId = bucketId, StreamId = streamId, MinRevision = minRevision, MaxRevision = maxRevision });
        return response.ResponseStream.ReadAllAsync(cancellationToken).Select(ToCommit);
    }

    private ICommit ToCommit(CommitInfo commit)
    {
        return new Commit(
            commit.BucketId,
            commit.StreamId,
            commit.StreamRevision,
            Guid.Parse(commit.CommitId),
            commit.CommitSequence,
            DateTimeOffset.FromUnixTimeSeconds(commit.CommitStamp),
            commit.CheckpointToken,
            commit.Headers.ToDictionary(x => x.Key,
                x => _serializer.Deserialize<object>(Convert.FromBase64String(x.Value))!),
            commit.Events.Select(ToEventMessage));
    }

    private string Serialize(object value)
    {
        using var stream = new MemoryStream();
        _serializer.Serialize(stream, value);
        return Convert.ToBase64String(stream.ToArray());
    }

    private EventMessageInfo Serialize(EventMessage message)
    {
        using var stream = new MemoryStream();
        _serializer.Serialize(stream, message.Body);
        var bytes = Convert.ToBase64String(stream.ToArray());
        return new EventMessageInfo
        {
            Headers =
            {
                message.Headers.ToDictionary(x => x.Key, x =>
                {
                    using var xStream = new MemoryStream();
                    _serializer.Serialize(xStream, x.Value);
                    return Convert.ToBase64String(xStream.ToArray());
                })
            },
            Base64Payload = bytes
        };
    }
}
