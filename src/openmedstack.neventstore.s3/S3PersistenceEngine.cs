﻿using System.Net;
using System.Runtime.CompilerServices;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.S3;

public class S3PersistenceEngine(
    IAmazonS3 context,
    IDetectConflicts conflictDetector,
    ISerialize serializer,
    string bucketName,
    ILogger<S3PersistenceEngine> logger) : ICommitEvents, IAccessSnapshots
{
    private const string SnapshotsTableName = "snapshots";
    private const string CommitsTableName = "commits";
    private bool _disposed;

    public void Dispose()
    {
        context.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async IAsyncEnumerable<ICommit> Get(
        string tenantId,
        string streamId,
        int minRevision = 0,
        int maxRevision = int.MaxValue,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowWhenDisposed();
        maxRevision = maxRevision == int.MaxValue ? int.MaxValue : maxRevision + 1;
        minRevision = minRevision < 0 ? 0 : minRevision;
        var prefix = $"{CommitsTableName}/{tenantId}/{streamId}/";
        var response = await context
            .ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Delimiter = "/",
                    Prefix = prefix
                }, cancellationToken).ConfigureAwait(false);
        if (response.S3Objects.Count == 0)
        {
            yield break;
        }

        var keys = response.S3Objects
            .Select(r => r.Key)
            .Where(key =>
            {
                var version = int.Parse(key.Split('_').Last().Replace(".json", ""));
                return minRevision <= version && version <= maxRevision;
            })
            .OrderBy(key => key)
            .ToList();
        foreach (var key in keys)
        {
            var obj = await context.GetObjectAsync(bucketName, key, cancellationToken).ConfigureAwait(false);
            if (obj.HttpStatusCode != HttpStatusCode.OK)
            {
                continue;
            }

            var commit = serializer.Deserialize<S3Commit>(obj.ResponseStream);
            if (commit == null)
            {
                continue;
            }

            yield return commit.ToCommit(serializer);
        }
    }

    public async Task<ICommit?> Commit(
        CommitAttempt commit,
        CancellationToken cancellationToken = default)
    {
        ThrowWhenDisposed();
        await CheckExists(commit.CommitSequence, commit).ConfigureAwait(false);
        var attempt = S3Commit.FromCommitAttempt(commit, serializer);
        var commitKey =
            $"{CommitsTableName}/{commit.TenantId}/{commit.StreamId}/{commit.CommitStamp.ToUnixTimeSeconds()}_{commit.CommitId}_{commit.CommitSequence}_{commit.StreamRevision}.json";
        using var stream = new MemoryStream();
        serializer.Serialize(stream, attempt);
        stream.Position = 0;
        var response = await context
            .PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = commitKey,
                    AutoCloseStream = true,
                    InputStream = stream
                },
                cancellationToken).ConfigureAwait(false);
        if (!response.HttpStatusCode.Equals(HttpStatusCode.OK))
        {
            return null;
        }

        return new Commit(
            attempt.TenantId,
            attempt.StreamId,
            attempt.StreamRevision,
            Guid.Parse(attempt.CommitId),
            attempt.CommitSequence + 1,
            DateTimeOffset.FromUnixTimeSeconds(attempt.CommitStamp),
            0,
            commit.Headers.ToDictionary(),
            commit.Events.ToList());
    }

    private void ThrowWhenDisposed()
    {
        if (!_disposed)
        {
            return;
        }

        logger.LogWarning("Accessing a disposed object");
        throw new ObjectDisposedException("Already disposed");
    }

    public async Task<ISnapshot?> GetSnapshot(
        string tenantId,
        string streamId,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        var queryRequest = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Delimiter = "/",
            Prefix = $"{SnapshotsTableName}/{tenantId}/{streamId}/"
        };
        var response = await context.ListObjectsV2Async(queryRequest, cancellationToken).ConfigureAwait(false);
        if (response.HttpStatusCode != HttpStatusCode.OK || response.S3Objects.Count == 0)
        {
            return null;
        }

        var version = response.S3Objects
            .Select(r =>
            {
                var key = r.Key.Split('/').Last().Replace(".json", "");
                var version = int.Parse(key.Split('_')[^1]);
                return (key, version);
            })
            .OrderByDescending(x => x.version)
            .SkipWhile(x => x.version > maxRevision)
            .First();
        var key = $"{SnapshotsTableName}/{tenantId}/{streamId}/{version.key}.json";
        var obj = await context.GetObjectAsync(bucketName, key, cancellationToken).ConfigureAwait(false);
        if (obj.HttpStatusCode != HttpStatusCode.OK)
        {
            return null;
        }

        var snapshot = serializer.Deserialize<S3Snapshot>(obj.ResponseStream);
        return snapshot?.ToSnapshot(serializer);
    }

    public async Task<bool> AddSnapshot(ISnapshot snapshot, CancellationToken cancellationToken)
    {
        var attempt = S3Snapshot.FromSnapshot(snapshot, serializer);
        var ms = new MemoryStream();
        await using var ms1 = ms.ConfigureAwait(false);
        serializer.Serialize(ms, attempt);
        ms.Position = 0;
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key =
                $"{SnapshotsTableName}/{snapshot.TenantId}/{snapshot.StreamId}/{snapshot.StreamRevision}.json",
            InputStream = ms,
            ContentType = "application/json"
        };
        var response = await context.PutObjectAsync(request, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.HttpStatusCode == HttpStatusCode.OK;
    }

    private async Task CheckExists(int commitSequence, CommitAttempt commit)
    {
        var response = await context.ListObjectsV2Async(
            new ListObjectsV2Request
            {
                Delimiter = "/",
                Prefix = $"{CommitsTableName}/{commit.TenantId}/{commit.StreamId}/",
                BucketName = bucketName
            }).ConfigureAwait(false);
        if (response.S3Objects.Count == 0)
        {
            return;
        }

        var keys = response.S3Objects
            .Select(r => r.Key)
            .ToList();
        keys.Sort();
        var commitId = commit.CommitId.ToString();
        foreach (var split in keys.Select(key => key.Split('_')))
        {
            if (commitId == split[1])
            {
                throw new DuplicateCommitException($"Commit {commit.CommitId} already exists");
            }

            var committingSequence = int.Parse(split[^2]);
            if (committingSequence == commitSequence)
            {
                throw new ConcurrencyException("Commit sequence already exists");
            }
            var committingRevision = int.Parse(split[^1].Replace(".json", ""));
            if (commit.StreamRevision <= committingRevision)
            {
                var overlapping = keys.Where(k =>
                        int.Parse(k.Split('_').Last().Replace(".json", "")) >= commit.StreamRevision)
                    .ToList();
                if (overlapping.Count == 0)
                {
                    return;
                }

                foreach (var o in overlapping.Select(FileToCommit))
                {
                    var c = await o.ConfigureAwait(false);
                    if (c == null)
                    {
                        continue;
                    }

                    var attempting = serializer.Deserialize<List<EventMessage>>(c.Events) ?? [];
                    if (conflictDetector.ConflictsWith(commit.Events, attempting))
                    {
                        throw new ConflictingCommitException($"Commit {commit.CommitId} conflicts with {c.CommitId}");
                    }

                    throw new NonConflictingCommitException(
                        $"Found non-conflicting commits at revision {commit.StreamRevision}");
                }
            }
        }
    }

    private async Task<S3Commit?> FileToCommit(string key)
    {
        var file = await context.GetObjectAsync(bucketName, key).ConfigureAwait(false);
        var doc = serializer.Deserialize<S3Commit>(file.ResponseStream);
        return doc;
    }
}
