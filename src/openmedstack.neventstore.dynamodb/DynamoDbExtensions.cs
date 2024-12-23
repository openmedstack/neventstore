using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.DynamoDb;

internal static class DynamoDbExtensions
{
    public static ICommit ToCommit(this Dictionary<string, AttributeValue> commit, ISerialize serializer)
    {
        return new Commit(
            commit["TenantId"].S,
            commit["StreamId"].S,
            int.Parse(commit["StreamRevision"].N),
            Guid.Parse(commit["CommitId"].S),
            int.Parse(commit["CommitSequence"].N),
            DateTimeOffset.FromUnixTimeSeconds(long.Parse(commit["CommitStamp"].N)),
            0,
            serializer.Deserialize<Dictionary<string, object>>(commit["Headers"].B),
            serializer.Deserialize<List<EventMessage>>(commit["Events"].B));
    }

    public static async Task<bool>
        Save<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        this IAmazonDynamoDB db,
        T item,
        bool overwriteExisting = false,
        CancellationToken cancellationToken = default)
    {
        if (item is null)
        {
            return false;
        }

        var type = typeof(T);
        var tableAttribute =
            type.GetCustomAttributes(typeof(DynamoDBTableAttribute), false).FirstOrDefault() as
                DynamoDBTableAttribute;
        var properties = type.GetProperties();
        var hashKey =
            properties.First(p => p.GetCustomAttributes(typeof(DynamoDBHashKeyAttribute), false).Length > 0);
        var rangeKey =
            properties.FirstOrDefault(p =>
                p.GetCustomAttributes(typeof(DynamoDBRangeKeyAttribute), false).Length > 0);
        if (tableAttribute == null)
        {
            throw new InvalidOperationException("DynamoDBTableAttribute not found on type");
        }

        var putItemRequest = new PutItemRequest
        {
            TableName = tableAttribute.TableName,
            Item = MapToDictionary(item),
            ReturnValues = ReturnValue.NONE,
            ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE
        };
        if (!overwriteExisting)
        {
            putItemRequest.ConditionExpression = rangeKey == null
                ? $"attribute_not_exists({hashKey.Name})"
                : $"attribute_not_exists({hashKey.Name}) AND attribute_not_exists({rangeKey.Name})";
        }

        var response = await db.PutItemAsync(putItemRequest, cancellationToken).ConfigureAwait(false);
        return response.HttpStatusCode == HttpStatusCode.OK;
    }

    private static Dictionary<string, AttributeValue> MapToDictionary<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]T>(T item)
    {
        var properties = typeof(T).GetProperties();
        return properties.Where(x => !x.GetCustomAttributes<DynamoDBIgnoreAttribute>().Any()).ToDictionary(
            p => p.Name,
            p =>
            {
                var value = p.GetValue(item);
                var isBoolean = p.PropertyType == typeof(bool);
                var isByteArray = p.PropertyType == typeof(byte[]);
                var isString = p.PropertyType == typeof(string);
                var isStringSet = typeof(IEnumerable<string>).IsAssignableFrom(p.PropertyType);
                var isNumericList = IsNumericList(p);
                var isMap = !p.PropertyType.IsValueType
                 && !isString
                 && !isStringSet
                 && !isByteArray
                 && !isNumericList;
                return value == null
                    ? new AttributeValue
                    {
                        NULL = true
                    }
                    : new AttributeValue
                    {
                        BOOL = isBoolean && Convert.ToBoolean(value),
                        IsBOOLSet = isBoolean,
                        B = isByteArray ? new MemoryStream((byte[])value) : null,
                        S = isString ? value as string : null,
                        N = IsNumeric(p) ? value.ToString() : null,
                        SS = isStringSet
                            ? ((IEnumerable<string>)value).ToList()
                            : null,
                        NS = isNumericList && !isByteArray
                            ? ((IEnumerable)value).OfType<object>()
                            .Select(x => x.ToString()).ToList()
                            : null,
                        M = isMap
                            ? MapToDictionary(value)
                            : null,
                        IsMSet = isMap,
                    };
            });
    }

    private static bool IsNumericList(PropertyInfo p)
    {
        return typeof(IEnumerable<int>).IsAssignableFrom(p.PropertyType)
         || typeof(IEnumerable<uint>).IsAssignableFrom(p.PropertyType)
         || typeof(IEnumerable<long>).IsAssignableFrom(p.PropertyType)
         || typeof(IEnumerable<ulong>).IsAssignableFrom(p.PropertyType)
         || typeof(IEnumerable<float>).IsAssignableFrom(p.PropertyType)
         || typeof(IEnumerable<double>).IsAssignableFrom(p.PropertyType)
         || typeof(IEnumerable<decimal>).IsAssignableFrom(p.PropertyType)
         || typeof(IEnumerable<short>).IsAssignableFrom(p.PropertyType)
         || typeof(IEnumerable<ushort>).IsAssignableFrom(p.PropertyType)
         || typeof(IEnumerable<sbyte>).IsAssignableFrom(p.PropertyType);
    }

    private static bool IsNumeric(PropertyInfo p)
    {
        return p.PropertyType.IsAssignableTo(typeof(int))
         || p.PropertyType.IsAssignableTo(typeof(uint))
         || p.PropertyType.IsAssignableTo(typeof(long))
         || p.PropertyType.IsAssignableTo(typeof(ulong))
         || p.PropertyType.IsAssignableTo(typeof(float))
         || p.PropertyType.IsAssignableTo(typeof(double))
         || p.PropertyType.IsAssignableTo(typeof(decimal))
         || p.PropertyType.IsAssignableTo(typeof(short))
         || p.PropertyType.IsAssignableTo(typeof(ushort))
         || p.PropertyType.IsAssignableTo(typeof(byte))
         || p.PropertyType.IsAssignableTo(typeof(sbyte));
    }
}
