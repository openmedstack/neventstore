using Newtonsoft.Json;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Serialization;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

internal class NesJsonSerializer : ISerialize
{
    private readonly ILogger _logger;

    private readonly IEnumerable<Type> _knownTypes = new[]
        { typeof(List<EventMessage>), typeof(Dictionary<string, object>) };

    private readonly JsonSerializerSettings _serializerOptions = new()
    {
        Formatting = Formatting.None, DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateParseHandling = DateParseHandling.DateTimeOffset, FloatFormatHandling = FloatFormatHandling.String,
        TypeNameHandling = TypeNameHandling.Auto, MetadataPropertyHandling = MetadataPropertyHandling.Default,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    public NesJsonSerializer(ILogger logger, params Type[] knownTypes)
    {
        _logger = logger;
        if (knownTypes.Length > 0)
        {
            _knownTypes = knownTypes;
        }

        foreach (var type in _knownTypes)
        {
            _logger.LogDebug(SerializerMessages.RegisteringKnownType, type);
        }
    }

    public virtual void Serialize<T>(Stream output, T graph)
    {
        _logger.LogTrace(Messages.SerializingGraph, typeof(T));
        using var streamWriter = new StreamWriter(output, Encoding.UTF8);
        var serializer = JsonSerializer.Create(_serializerOptions);
        serializer.Serialize(streamWriter, graph, typeof(T));
    }

    public virtual T? Deserialize<T>(Stream input)
    {
        _logger.LogTrace(Messages.DeserializingStream, typeof(T));
        using var streamReader = new StreamReader(input, Encoding.UTF8);
        using var jsonReader = new JsonTextReader(streamReader);
        var serializer = JsonSerializer.Create(_serializerOptions);
        return serializer.Deserialize<T>(jsonReader);
    }

    public T? Deserialize<T>(byte[] input)
    {
        using var stream = new MemoryStream(input);
        return Deserialize<T>(stream);
    }
}