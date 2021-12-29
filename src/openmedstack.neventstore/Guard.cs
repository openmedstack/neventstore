namespace OpenMedStack.NEventStore
{
    using System;
    using System.Collections.Generic;

    internal static class Guard
    {
        internal static void NotNullOrWhiteSpace(string parameterName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Cannot be null or whitespace", parameterName);
            }
        }


        internal static void NotNull<T>(string paramName, T value)
        {
            if (ReferenceEquals(value, null))
            {
                throw new ArgumentNullException(paramName);
            }
        }

        internal static void NotLessThanOrEqualTo<T>(string paramName, T value, T compareTo)
            where T : IComparable
        {
            NotNull(paramName, value);
            if (value.CompareTo(compareTo) <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    $"{paramName} has value {value} which is less than or equal to {compareTo}");
            }
        }

        internal static void NotLessThan<T>(string paramName, T value, T compareTo)
            where T : IComparable
        {
            NotNull(paramName, value);
            if (value.CompareTo(compareTo) < 0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    $"{paramName} has value {value} which is less than {compareTo}");
            }
        }

        internal static void NotDefault<T>(string paramName, T value)
            where T : IComparable
        {
            NotNull(paramName, value);
            if (value.CompareTo(default(T)) == 0)
            {
                throw new ArgumentException(
                    $"{paramName} has value {value} which cannot be equal to it's default value {default(T)}",
                    paramName);
            }
        }

        internal static void NotEmpty<T>(string paramName, ICollection<T> value)
        {
            NotNull(paramName, value);
            if (value.Count == 0)
            {
                throw new ArgumentException($"{paramName} cannot be empty", paramName);
            }
        }
    }
}
