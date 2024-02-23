using Amazon.DynamoDBv2;
using Amazon.Runtime;
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
