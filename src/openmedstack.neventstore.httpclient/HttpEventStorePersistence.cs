using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.HttpClient;

using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

/// <summary>
/// Defines the HTTP based implementation of <see cref="ICommitEvents"/> and <see cref="IAccessSnapshots"/>.
/// </summary>
internal class HttpEventStorePersistence : ICommitEvents, IAccessSnapshots
{
    private const string ApplicationJson = "application/json";

    private readonly HttpClient _client;
    private readonly ISerialize _serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpEventStorePersistence"/> class.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> to use.</param>
    /// <param name="serializer">The <see cref="ISerialize"/> to use.</param>
    public HttpEventStorePersistence(HttpClient client, ISerialize serializer)
    {
        _client = client;
        _serializer = serializer;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(ApplicationJson));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _client.CancelPendingRequests();
        _client.Dispose();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ICommit> Get(
        string tenantId,
        string streamId,
        int minRevision = 0,
        int maxRevision = int.MaxValue,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await _client.GetAsync(
            $"commits/{tenantId}/{streamId}/{minRevision}/{maxRevision}",
            cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var commits =
                content.DeserializeAsyncEnumerable<Commit>(cancellationToken: cancellationToken);
            await foreach (var commit in commits.ConfigureAwait(false))
            {
                if (commit != null)
                {
                    yield return ToDeserializedCommit(commit);
                }
            }
        }
        else
        {
            throw new Exception(response.ReasonPhrase);
        }
    }

    private Commit ToDeserializedCommit(Commit commit)
    {
        return new Commit(commit.TenantId, commit.StreamId, commit.StreamRevision, commit.CommitId,
            commit.CommitSequence, commit.CommitStamp, commit.CheckpointToken, commit.Headers,
            commit.Events.Select(x => new EventMessage(
                _serializer.Deserialize<object>(Convert.FromBase64String((string)x.Body))!,
                x.Headers.ToDictionary(y => y.Key,
                    y => JsonConvert.DeserializeObject((string)y.Value))!)));
    }

    /// <inheritdoc />
    public async Task<ICommit?> Commit(CommitAttempt eventStream, CancellationToken cancellationToken)
    {
        if (eventStream.Events.Count == 0)
        {
            return null;
        }

        var commitAttempt = new CommitAttempt(
            eventStream.TenantId,
            eventStream.StreamId,
            eventStream.StreamRevision,
            eventStream.CommitId,
            eventStream.CommitSequence + 1,
            DateTimeOffset.UtcNow,
            eventStream.Headers.ToDictionary(),
            eventStream.Events.Select(x => new EventMessage(SerializeBody(x.Body),
                x.Headers.ToDictionary(y => y.Key, y => (object)JsonConvert.SerializeObject(y.Value)))).ToList());
        var response = await _client.PostAsync(
                "/commit",
                new StringContent(JsonConvert.SerializeObject(commitAttempt), Encoding.UTF8, ApplicationJson),
                cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(response.ReasonPhrase);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var commit = await stream
            .DeserializeAsync<Commit>(cancellationToken: CancellationToken.None).ConfigureAwait(false);
        return commit == null ? commit : ToDeserializedCommit(commit);

        string SerializeBody(object body)
        {
            using var s = new MemoryStream();
            _serializer.Serialize(s, body);
            s.Flush();
            var bytes = Convert.ToBase64String(s.ToArray());
            return bytes;
        }
    }

    /// <inheritdoc />
    public async Task<ISnapshot?> GetSnapshot(
        string tenantId,
        string streamId,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        var response = await _client.GetAsync($"snapshots/{tenantId}/{streamId}/{maxRevision}", cancellationToken)
            .ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var snapshot = await (await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                .DeserializeAsync<Snapshot>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return snapshot;
        }

        throw new Exception(response.ReasonPhrase);
    }

    /// <inheritdoc />
    public async Task<bool> AddSnapshot(ISnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsync("snapshots", new StringContent(
            JsonConvert.SerializeObject(snapshot),
            Encoding.UTF8,
            ApplicationJson), cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<IStreamHead> GetStreamsToSnapshot(
        string bucketId,
        int maxThreshold,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await _client.GetAsync($"streams/{bucketId}/{maxThreshold}", cancellationToken)
            .ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var streamHeads = content.DeserializeAsyncEnumerable<StreamHead>(cancellationToken: cancellationToken);
            await foreach (var streamHead in streamHeads.ConfigureAwait(false))
            {
                if (streamHead != null)
                {
                    yield return streamHead;
                }
            }
        }
    }
}
