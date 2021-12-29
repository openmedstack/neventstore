namespace OpenMedStack.NEventStore.Persistence.Sql
{
    using System;
    using System.Data;
    using Microsoft.Extensions.Logging;

    internal static class ExtensionMethods
    {
        public static Guid ToGuid(this object value)
        {
            if (value is Guid guid)
            {
                return guid;
            }

            return value is byte[] bytes ? new Guid(bytes) : Guid.Empty;
        }

        public static int ToInt(this object value) =>
            value is int i
                ? i
                : value is long l
                    ? (int)l
                    : value is decimal i1 ? (int)i1 : Convert.ToInt32(value);

        public static long ToLong(this object value) =>
            value is long l
                ? l
                : value is int i
                    ? i
                    : value is decimal l1 ? (long)l1 : Convert.ToInt32(value);

        public static IDbCommand SetParameter(this IDbCommand command, string name, object? value, DbType? parameterType = null, ILogger? logger = null)
        {
            logger?.LogTrace("Rebinding parameter '{0}' with value: {1}", name, value);
            var parameter = (IDataParameter)command.Parameters[name];
            parameter.Value = value;
            if (parameterType.HasValue)
            {
                parameter.DbType = parameterType.Value;
            }

            return command;
        }
    }
}