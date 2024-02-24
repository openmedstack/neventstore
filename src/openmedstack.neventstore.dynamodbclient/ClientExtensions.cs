using Amazon.DynamoDBv2.Model;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.DynamoDbClient;

internal static class ClientExtensions
{
    public static ICommit ToCommit(this Dictionary<string, AttributeValue> commit, ISerialize serializer)
    {
        return new Commit(
            commit["BucketId"].S,
            commit["StreamId"].S,
            int.Parse(commit["StreamRevision"].N),
            Guid.Parse(commit["CommitId"].S),
            int.Parse(commit["CommitSequence"].N),
            DateTimeOffset.FromUnixTimeSeconds(long.Parse(commit["CommitStamp"].N)),
            0,
            serializer.Deserialize<Dictionary<string, object>>(commit["Headers"].B),
            serializer.Deserialize<List<EventMessage>>(commit["Events"].B));
    }
}
