using System.Text;
using Newtonsoft.Json;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Server.Tests.Steps;

internal class TestJsonSerializer : ISerialize
{
    private readonly JsonSerializerSettings _serializerOptions = new()
    {
        Formatting = Formatting.None, DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateParseHandling = DateParseHandling.DateTimeOffset, FloatFormatHandling = FloatFormatHandling.String,
        TypeNameHandling = TypeNameHandling.Auto, MetadataPropertyHandling = MetadataPropertyHandling.Default,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    public virtual void Serialize<T>(Stream output, T graph)
    {
        using var streamWriter = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);
        var serializer = JsonSerializer.Create(_serializerOptions);
        serializer.Serialize(streamWriter, graph, typeof(T));
    }

    public virtual T? Deserialize<T>(Stream input)
    {
        using var streamReader = new StreamReader(input, Encoding.UTF8);
        using var jsonReader = new JsonTextReader(streamReader);
        var serializer = JsonSerializer.Create(_serializerOptions);
        return serializer.Deserialize<T>(jsonReader);
    }

    public T? Deserialize<T>(byte[] input)
    {
        using var stream = new MemoryStream(input);
        var streamReader = new StreamReader(stream);
        var jsonReader = new JsonTextReader(streamReader);
        var serializer = JsonSerializer.Create(_serializerOptions);
        return serializer.Deserialize<T>(jsonReader);
    }
}
