using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    internal class NesJsonSerializer : ISerialize
    {
        private readonly ILogger _logger;
        private readonly IEnumerable<Type> _knownTypes = new[] { typeof(List<EventMessage>), typeof(Dictionary<string, object>) };

        private readonly JsonSerializer _typedSerializer = new()
        {
            TypeNameHandling = TypeNameHandling.All,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly JsonSerializer _untypedSerializer = new()
        {
            TypeNameHandling = TypeNameHandling.All,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
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
            Serialize(new JsonTextWriter(streamWriter), graph);
        }

        public virtual T? Deserialize<T>(Stream input)
        {
            _logger.LogTrace(Messages.DeserializingStream, typeof(T));
            using var streamReader = new StreamReader(input, Encoding.UTF8);
            return Deserialize<T>(new JsonTextReader(streamReader));
        }

        public virtual T? Deserialize<T>(byte[] input)
        {
            using var ms = new MemoryStream(input, false);
            return Deserialize<T>(ms);
        }

        protected virtual void Serialize<T>(JsonWriter writer, T graph)
        {
            using (writer)
            {
                GetSerializer(typeof(T)).Serialize(writer, graph);
            }
        }

        protected virtual T? Deserialize<T>(JsonReader reader)
        {
            var type = typeof(T);

            using (reader)
            {
                var item = GetSerializer(type).Deserialize(reader, type);
                return item == null ? default : (T) item;
            }
        }

        protected virtual JsonSerializer GetSerializer(Type typeToSerialize)
        {
            if (_knownTypes.Contains(typeToSerialize))
            {
                _logger.LogTrace(SerializerMessages.UsingUntypedSerializer, typeToSerialize);
                return _untypedSerializer;
            }

            _logger.LogTrace(SerializerMessages.UsingTypedSerializer, typeToSerialize);
            return _typedSerializer;
        }
    }
}
