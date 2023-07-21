using System;
using System.Collections.Generic;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Persistence;

public sealed class StreamHeadEqualityComparer : IEqualityComparer<IStreamHead>
{
    public bool Equals(IStreamHead? x, IStreamHead? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }
        if (x is null)
        {
            return false;
        }
        if (y is null)
        {
            return false;
        }
        if (x.GetType() != y.GetType())
        {
            return false;
        }
        return string.Equals(x.StreamId, y.StreamId) && string.Equals(x.BucketId, y.BucketId);
    }

    public int GetHashCode(IStreamHead obj)
    {
        return HashCode.Combine(obj.StreamId, obj.BucketId);
    }
}