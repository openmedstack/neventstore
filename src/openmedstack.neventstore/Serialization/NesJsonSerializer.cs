using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using Newtonsoft.Json;
using OpenMedStack.NEventStore.Abstractions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace OpenMedStack.NEventStore.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Extensions.Logging;

    internal class NesJsonSerializer : ISerialize
    {
        private readonly ILogger _logger;
        private readonly IEnumerable<Type> _knownTypes = new[] { typeof(List<EventMessage>), typeof(Dictionary<string, object>) };

        private readonly JsonSerializerOptions _serializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString |
                JsonNumberHandling.AllowNamedFloatingPointLiterals,
            AllowTrailingCommas = true,
            TypeInfoResolver = DataContractResolver.Default
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
            JsonSerializer.Serialize(output, graph, _serializerOptions);
        }

        public virtual T? Deserialize<T>(Stream input)
        {
            _logger.LogTrace(Messages.DeserializingStream, typeof(T));
            using var streamReader = new StreamReader(input, Encoding.UTF8);
            return JsonSerializer.Deserialize<T>(input, _serializerOptions);
        }

        public T? Deserialize<T>(byte[] input)
        {
            using var stream = new MemoryStream(input);
            return Deserialize<T>(stream);
        }
    }

    public class DataContractResolver : IJsonTypeInfoResolver
    {
        private static DataContractResolver? _defaultInstance;

        public static DataContractResolver Default
        {
            get
            {
                if (_defaultInstance is { } result)
                {
                    return result;
                }

                DataContractResolver newInstance = new();
                DataContractResolver? originalInstance = Interlocked.CompareExchange(ref _defaultInstance, newInstance, comparand: null);
                return originalInstance ?? newInstance;
            }
        }

        private static bool IsNullOrDefault(object? obj)
        {
            if (obj is null)
            {
                return true;
            }

            Type type = obj.GetType();

            return type.IsValueType && FormatterServices.GetUninitializedObject(type).Equals(obj);
        }

        private static IEnumerable<MemberInfo> EnumerateFieldsAndProperties(Type type, BindingFlags bindingFlags)
        {
            foreach (FieldInfo fieldInfo in type.GetFields(bindingFlags))
            {
                yield return fieldInfo;
            }

            foreach (PropertyInfo propertyInfo in type.GetProperties(bindingFlags))
            {
                yield return propertyInfo;
            }
        }

        private static IEnumerable<JsonPropertyInfo> CreateDataMembers(JsonTypeInfo jsonTypeInfo)
        {
            bool isDataContract = jsonTypeInfo.Type.GetCustomAttribute<DataContractAttribute>() != null;
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public;

            if (isDataContract)
            {
                bindingFlags |= BindingFlags.NonPublic;
            }

            foreach (MemberInfo memberInfo in EnumerateFieldsAndProperties(jsonTypeInfo.Type, bindingFlags))
            {
                DataMemberAttribute? attr = null;
                if (isDataContract)
                {
                    attr = memberInfo.GetCustomAttribute<DataMemberAttribute>();
                    if (attr == null)
                    {
                        continue;
                    }
                }
                else
                {
                    if (memberInfo.GetCustomAttribute<IgnoreDataMemberAttribute>() != null)
                    {
                        continue;
                    }
                }

                if (memberInfo == null)
                {
                    continue;
                }

                Func<object, object?>? getValue = null;
                Action<object, object?>? setValue = null;
                Type? propertyType = null;
                string? propertyName = null;

                if (memberInfo.MemberType == MemberTypes.Field && memberInfo is FieldInfo fieldInfo)
                {
                    propertyName = attr?.Name ?? fieldInfo.Name;
                    propertyType = fieldInfo.FieldType;
                    getValue = fieldInfo.GetValue;
                    setValue = (obj, value) => fieldInfo.SetValue(obj, value);
                }
                else
                if (memberInfo.MemberType == MemberTypes.Property && memberInfo is PropertyInfo propertyInfo)
                {
                    propertyName = attr?.Name ?? propertyInfo.Name;
                    propertyType = propertyInfo.PropertyType;
                    if (propertyInfo.CanRead)
                    {
                        getValue = propertyInfo.GetValue;
                    }
                    if (propertyInfo.CanWrite)
                    {
                        setValue = (obj, value) => propertyInfo.SetValue(obj, value);
                    }
                }
                else
                {
                    continue;
                }

                JsonPropertyInfo jsonPropertyInfo = jsonTypeInfo.CreateJsonPropertyInfo(propertyType, propertyName);
                if (jsonPropertyInfo == null)
                {
                    continue;
                }

                jsonPropertyInfo.Get = getValue;
                jsonPropertyInfo.Set = setValue;

                if (attr != null)
                {
                    jsonPropertyInfo.Order = attr.Order;
                    jsonPropertyInfo.ShouldSerialize = !attr.EmitDefaultValue ? ((_, obj) => !IsNullOrDefault(obj)) : null;
                }

                yield return jsonPropertyInfo;
            }
        }

        public static JsonTypeInfo GetTypeInfo(JsonTypeInfo jsonTypeInfo)
        {
            if (jsonTypeInfo.Kind == JsonTypeInfoKind.Object)
            {
                jsonTypeInfo.CreateObject = () => Activator.CreateInstance(jsonTypeInfo.Type)!;

                foreach (var jsonPropertyInfo in CreateDataMembers(jsonTypeInfo).OrderBy((x) => x.Order))
                {
                    jsonTypeInfo.Properties.Add(jsonPropertyInfo);
                }
            }

            return jsonTypeInfo;
        }

        public JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo jsonTypeInfo = JsonTypeInfo.CreateJsonTypeInfo(type, options);

            return GetTypeInfo(jsonTypeInfo);
        }
    }
}
