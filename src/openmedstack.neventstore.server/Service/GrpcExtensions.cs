using Newtonsoft.Json;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Grpc;

namespace OpenMedStack.NEventStore.Server.Service;

internal static class GrpcExtensions
{
    public static EventMessageInfo ToEventMessageInfo(this EventMessage message)
    {
        return new EventMessageInfo
        {
            Headers = { message.Headers.ToDictionary(x => x.Key, x => JsonConvert.SerializeObject(x.Value)) },
            Base64Payload = message.Body as string
        };
    }
}
