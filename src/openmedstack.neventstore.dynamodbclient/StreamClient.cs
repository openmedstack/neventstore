using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using OpenMedStack.Events;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.DynamoDbClient;

public sealed class DelegateStreamClient(
    AWSCredentials credentials,
    AmazonDynamoDBStreamsConfig config,
    Func<EventMessage, CancellationToken, Task> handler,
    ISerialize serializer)
    : StreamClient(credentials, config, serializer)
{
    protected override async Task Handle(EventMessage message, CancellationToken cancellationToken)
    {
        await handler(message, cancellationToken);
    }
}

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
        foreach (var shard in describeStreamResponse.StreamDescription.Shards)
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
                           let memoryStream = record.Dynamodb.NewImage["Events"].B
                           let events = _serializer.Deserialize<EventMessage[]>(memoryStream)
                           where events != null
                           from @event in events
                           select @event;
            foreach (var message in messages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Handle(message, cancellationToken);
            }
        }
    }

    protected abstract Task Handle(EventMessage message, CancellationToken cancellationToken);
}
