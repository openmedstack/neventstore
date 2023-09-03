using Newtonsoft.Json;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Serialization;

using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

internal class NesJsonSerializer : ISerialize
{
    private readonly ILogger<NesJsonSerializer> _logger;
    private readonly JsonSerializer _jsonSerializer;

    private readonly JsonSerializerSettings _serializerOptions = new()
    {
        Formatting = Formatting.None, DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateParseHandling = DateParseHandling.DateTimeOffset, FloatFormatHandling = FloatFormatHandling.String,
        TypeNameHandling = TypeNameHandling.Auto, MetadataPropertyHandling = MetadataPropertyHandling.Default,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    public NesJsonSerializer(ILogger<NesJsonSerializer> logger)
    {
        _logger = logger;
        _jsonSerializer = JsonSerializer.Create(_serializerOptions);
    }

    public virtual void Serialize<T>(Stream output, T graph)
    {
        _logger.LogTrace(Messages.SerializingGraph, typeof(T));
        using var streamWriter = new StreamWriter(output, Encoding.UTF8);
        _jsonSerializer.Serialize(streamWriter, graph, typeof(T));
    }

    public virtual T? Deserialize<T>(Stream input)
    {
        _logger.LogTrace(Messages.DeserializingStream, typeof(T));
        using var streamReader = new StreamReader(input, Encoding.UTF8);
        using var jsonReader = new JsonTextReader(streamReader);
        return _jsonSerializer.Deserialize<T>(jsonReader);
    }

    public T? Deserialize<T>(byte[] input)
    {
        using var stream = new MemoryStream(input);
        return Deserialize<T>(stream);
    }
}
