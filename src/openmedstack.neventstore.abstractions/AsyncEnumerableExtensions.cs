// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AsyncEnumerableExtensions.cs" company="Reimers.dk">
//   Copyright � Reimers.dk
// </copyright>
// <summary>
//   Defines the AsyncEnumerableExtensions type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace OpenMedStack.NEventStore.Abstractions;

public static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToNonNullAsyncEnumerable<T>(this T? item)
    {
        await Task.Yield();
        if (item != null)
        {
            yield return item;
        }
    }

    public static async IAsyncEnumerable<T> Empty<T>()
    {
        await Task.Yield();
        yield break;
    }

    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        this IEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        foreach (var item in source)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            yield return item;
        }
    }

    public static async Task<List<T>> ToList<T>(
        this IAsyncEnumerable<T> enumerable,
        CancellationToken cancellationToken = default)
    {
        var list = new List<T>();
        await foreach (var item in enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            list.Add(item);
        }

        return list;
    }

    public static async Task<T[]> ToArray<T>(
        this IAsyncEnumerable<T> enumerable,
        CancellationToken cancellationToken = default)
    {
        var list = await enumerable.ToList(cancellationToken).ConfigureAwait(false);
        return list.ToArray();
    }

    public static async Task<T> First<T>(
        this IAsyncEnumerable<T> enumerable,
        CancellationToken cancellationToken = default)
    {
        await foreach (var item in enumerable.ConfigureAwait(false).WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            return item;
        }

        throw new InvalidOperationException();
    }

    public static async Task<T> Single<T>(
        this IAsyncEnumerable<T> enumerable,
        CancellationToken cancellationToken = default)
    {
        byte count = 0;
        T? result = default;
        await foreach (var item in enumerable.ConfigureAwait(false).WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            if (count++ > 1)
            {
                throw new InvalidOperationException();
            }

            result = item;
        }

        return result switch
        {
            null => throw new InvalidOperationException(),
            _ when count == 1 => result!,
            _ => throw new InvalidOperationException()
        };
    }

    public static async Task<T?> FirstOrDefault<T>(
        this IAsyncEnumerable<T> enumerable,
        CancellationToken cancellationToken)
    {
        await foreach (var item in enumerable.ConfigureAwait(false).WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            return item;
        }

        return default;
    }

    public static async Task<T?> FirstOrDefault<T>(
        this IAsyncEnumerable<T> enumerable,
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        await foreach (var item in enumerable.ConfigureAwait(false).WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            if (predicate(item))
            {
                return item;
            }
        }

        return default;
    }

    public static async Task<int> Count<T>(
        this IAsyncEnumerable<T> enumerable,
        CancellationToken cancellationToken = default)
    {
        var result = 0;
        await foreach (var _ in enumerable.ConfigureAwait(false).WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            result++;
        }

        return result;
    }
}
