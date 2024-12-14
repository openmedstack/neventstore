using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.S3;

public class S3Management(IAmazonS3 dbClient, string bucketName, ILogger<S3Management> logger) : IManagePersistence
{
    private const string CommitsTableName = "commits";
    private const string SnapshotsTableName = "snapshots";

    public async Task Initialize()
    {
        await Task.Yield();
        await dbClient.PutBucketAsync(new PutBucketRequest { BucketName = bucketName, UseClientRegion = true });
    }

    public IAsyncEnumerable<ICommit> GetFrom(string bucketId, long checkpointToken, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public IAsyncEnumerable<IStreamHead> GetStreamsToSnapshot(
        string bucketId,
        int maxThreshold,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<bool> Purge(string bucketId)
    {
        return Task.FromResult(true);
    }

    public async Task<bool> Drop()
    {
        logger.LogInformation("Dropping tables {Commits} and {Snapshots} in tenant: {BucketName}", CommitsTableName,
            SnapshotsTableName, bucketName);
        var response = await dbClient.DeleteBucketAsync(bucketName).ConfigureAwait(false);
        return response.HttpStatusCode == HttpStatusCode.OK;
    }
}
