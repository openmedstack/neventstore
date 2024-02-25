using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.DynamoDbClient;

/// <summary>
/// Defines the abstract DynamoDb stream client.
/// </summary>
/// <param name="credentials">The credentials for accessing the database.</param>
/// <param name="config">The client configuration.</param>
/// <param name="serializer">The <see cref="ISerialize"/> to use.</param>
/// <param name="logger">The <see cref="ILogger{TCategoryName}"/>to use.</param>
public abstract class StreamClient(
    AWSCredentials credentials,
    AmazonDynamoDBStreamsConfig config,
    ISerialize serializer,
    ILogger<StreamClient> logger)
{
    private readonly AmazonDynamoDBStreamsClient _client = new(credentials, config);
    private bool _stopped;

    /// <summary>
    /// Initiates a new connection to the database and starts receiving for new commits.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use for the async operation.</param>
    public async Task Subscribe(CancellationToken cancellationToken = default)
    {
        var streams = await _client.ListStreamsAsync(cancellationToken);
        var streamArn = streams.Streams.First(x => x.TableName == "commits").StreamArn;
        var describeStreamResponse =
            await _client.DescribeStreamAsync(
                new DescribeStreamRequest
                    { StreamArn = streamArn }, cancellationToken);
        foreach (var shard in describeStreamResponse.StreamDescription.Shards
            .TakeWhile(_ => !_stopped))
        {
            var shardIterator = await _client.GetShardIteratorAsync(new GetShardIteratorRequest
            {
                StreamArn = streamArn,
                ShardId = shard.ShardId,
                ShardIteratorType = ShardIteratorType.TRIM_HORIZON
            }, cancellationToken);
            if (shardIterator == null)
            {
                break;
            }

            var records = await _client.GetRecordsAsync(new GetRecordsRequest
            {
                ShardIterator = shardIterator.ShardIterator
            }, cancellationToken);
            var messages = from record in records.Records
                           let image = record.Dynamodb.NewImage
                           select image.ToCommit(serializer);
            foreach (var message in messages)
            {
                if (!await InnerHandle(message, cancellationToken))
                {
                    Stop();
                }
            }
        }
    }

    private async Task<bool> InnerHandle(ICommit message, CancellationToken cancellationToken)
    {
        var result = await Handler(message, cancellationToken);
        switch (result)
        {
            case HandlingResult.MoveToNext:
                return true;
            case HandlingResult.Retry:
                logger.LogInformation("Retrying commit {CommitId}", message.CommitId);
                return await InnerHandle(message, cancellationToken);
            case HandlingResult.Stop:
                logger.LogError("Error in dispatching commit. Received result: {Result}", result);
                return false;
            default:
                throw new ArgumentOutOfRangeException($"Unexpected result {result}");
        }
    }

    /// <summary>
    /// Stops the client from receiving new commits.
    /// </summary>
    public virtual void Stop()
    {
        _stopped = true;
    }

    /// <summary>
    /// The handler to use for processing new commits.
    /// </summary>
    /// <param name="commit">The <see cref="ICommit"/> to handle.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use for the async operation.</param>
    /// <returns>The <see cref="HandlingResult"/> from the processing.</returns>
    protected abstract Task<HandlingResult> Handler(ICommit commit, CancellationToken cancellationToken);
}
