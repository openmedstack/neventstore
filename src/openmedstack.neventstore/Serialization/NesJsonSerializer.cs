namespace OpenMedStack.NEventStore.Serialization;

using Newtonsoft.Json;
using OpenMedStack.NEventStore.Abstractions;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// The default JSON serializer for the NEventStore.
/// </summary>
public class NesJsonSerializer : ISerialize
{
    private readonly ILogger<NesJsonSerializer> _logger;
    private readonly JsonSerializer _jsonSerializer;

    private readonly JsonSerializerSettings _serializerOptions = new()
    {
        Formatting = Formatting.None, DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateParseHandling = DateParseHandling.DateTimeOffset, FloatFormatHandling = FloatFormatHandling.String,
        TypeNameHandling = TypeNameHandling.Objects, MetadataPropertyHandling = MetadataPropertyHandling.Default,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="NesJsonSerializer"/> class.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> to use.</param>
    public NesJsonSerializer(ILogger<NesJsonSerializer> logger)
    {
        _logger = logger;
        _jsonSerializer = JsonSerializer.Create(_serializerOptions);
    }

    /// <inheritdoc />
    public virtual void Serialize<T>(Stream output, T graph)
    {
        _logger.LogTrace(Messages.SerializingGraph, typeof(T));
        using var streamWriter = new StreamWriter(output, Encoding.UTF8);
        _jsonSerializer.Serialize(streamWriter, graph, typeof(T));
        streamWriter.Flush();
    }

    /// <inheritdoc />
    public virtual T? Deserialize<T>(Stream input)
    {
        _logger.LogTrace(Messages.DeserializingStream, typeof(T));
        using var streamReader = new StreamReader(input, Encoding.UTF8);
        using var jsonReader = new JsonTextReader(streamReader);
        return _jsonSerializer.Deserialize<T>(jsonReader);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(byte[] input)
    {
        _logger.LogTrace(Messages.DeserializingStream, typeof(T));
        using var stream = new MemoryStream(input);
        var streamReader = new StreamReader(stream);
        var json = streamReader.ReadToEnd();
        var jsonReader = new JsonTextReader(new StringReader(json));
        return _jsonSerializer.Deserialize<T>(jsonReader);
    }
}
