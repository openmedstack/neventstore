using Grpc.Core;
using Grpc.Net.Client;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using OpenMedStack.NEventStore.Grpc;

namespace OpenMedStack.NEventStore.GrpcClient;

internal class GrpcEventStorePersistence : IPersistStreams
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
        IsDisposed = true;
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
    public async Task<bool> AddSnapshot(ISnapshot snapshot)
    {
        var response = await _client.AddSnapshotAsync(null);
        return response.Value;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<IStreamHead> GetStreamsToSnapshot(
        string bucketId,
        int maxThreshold,
        CancellationToken cancellationToken)
    {
        var response = _client.GetStreamsToSnapshot(new StreamToSnapshotRequest
            { BucketId = bucketId, MaxRevision = maxThreshold });
        return response.ResponseStream.ReadAllAsync(cancellationToken).Select(head => new StreamHead(head.BucketId,
            head.StreamId,
            head.HeadRevision, head.SnapshotRevision));
    }

    /// <inheritdoc />
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public Task Initialize()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ICommit> GetFrom(
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

    /// <inheritdoc />
    public IAsyncEnumerable<ICommit> GetFrom(
        string bucketId,
        DateTimeOffset start,
        CancellationToken cancellationToken = default)
    {
        var response = _client.GetFromTimestamp(new GetFromTimestampRequest
            { BucketId = bucketId, Timestamp = start.ToUnixTimeSeconds() });
        return response.ResponseStream.ReadAllAsync(cancellationToken).Select(ToCommit);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ICommit> GetFrom(
        string bucketId,
        long checkpointToken,
        CancellationToken cancellationToken)
    {
        var response = _client.GetFromCheckpointToken(new GetFromCheckpointTokenRequest
            { BucketId = bucketId, CheckpointToken = checkpointToken }, cancellationToken: cancellationToken);
        return response.ResponseStream.ReadAllAsync(cancellationToken).Select(ToCommit);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ICommit> GetFromTo(
        string bucketId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var response = _client.GetFromTo(new GetFromToTimestampsRequest
            { BucketId = bucketId, From = start.ToUnixTimeSeconds(), To = end.ToUnixTimeSeconds() });
        return response.ResponseStream.ReadAllAsync(cancellationToken).Select(ToCommit);
    }

    /// <inheritdoc />
    public Task<bool> Purge()
    {
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public async Task<bool> Purge(string bucketId)
    {
        var response = await _client.PurgeAsync(new PurgeRequest { BucketId = bucketId }).ConfigureAwait(false);
        return response.Value;
    }

    /// <inheritdoc />
    public Task<bool> Drop()
    {
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteStream(string bucketId, string streamId)
    {
        var response = await _client.DeleteStreamAsync(new DeleteStreamRequest
            { BucketId = bucketId, StreamId = streamId }).ConfigureAwait(false);
        return response.Value;
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
