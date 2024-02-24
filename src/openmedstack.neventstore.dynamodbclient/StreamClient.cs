using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.DynamoDbClient;

public abstract class StreamClient
{
    private readonly ISerialize _serializer;
    private readonly AmazonDynamoDBStreamsClient _client;

    protected StreamClient(
        AWSCredentials credentials,
        AmazonDynamoDBStreamsConfig config,
        ISerialize serializer)
    {
        _serializer = serializer;
        _client = new AmazonDynamoDBStreamsClient(credentials, config);
    }

    public async Task Subscribe(CancellationToken cancellationToken)
    {
        var streams = await _client.ListStreamsAsync(cancellationToken);
        var streamArn = streams.Streams.First(x => x.TableName == "commits").StreamArn;
        var describeStreamResponse =
            await _client.DescribeStreamAsync(
                new DescribeStreamRequest
                    { StreamArn = streamArn }, cancellationToken);
        foreach (var shard in describeStreamResponse.StreamDescription.Shards
            .TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        {
            var shardIterator = await _client.GetShardIteratorAsync(new GetShardIteratorRequest
            {
                StreamArn = streamArn,
                ShardId = shard.ShardId,
                ShardIteratorType = ShardIteratorType.TRIM_HORIZON
            }, cancellationToken);
            var records = await _client.GetRecordsAsync(new GetRecordsRequest
            {
                ShardIterator = shardIterator.ShardIterator
            }, cancellationToken);
            var messages = from record in records.Records
                           let image = record.Dynamodb.NewImage
                           select image.ToCommit(_serializer);
            foreach (var message in messages)
            {
                await Handle(message, cancellationToken);
            }
        }
    }

    protected abstract Task Handle(ICommit message, CancellationToken cancellationToken);
}
