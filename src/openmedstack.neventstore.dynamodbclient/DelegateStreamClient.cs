using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.DynamoDbClient;

public sealed class DelegateStreamClient(
    AWSCredentials credentials,
    AmazonDynamoDBStreamsConfig config,
    Func<ICommit, CancellationToken, Task<HandlingResult>> handler,
    ISerialize serializer,
    ILogger<DelegateStreamClient> logger)
    : StreamClient(credentials, config, serializer, logger)
{
    protected override Task<HandlingResult> Handler(ICommit message, CancellationToken cancellationToken)
    {
        return handler(message, cancellationToken);
    }
}
