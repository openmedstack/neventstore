using System;
using System.Collections.Generic;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore;

public sealed class CommitEqualityComparer : IEqualityComparer<ICommit>
{
    public bool Equals(ICommit? x, ICommit? y)
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

        return string.Equals(x.BucketId, y.BucketId) && string.Equals(x.StreamId, y.StreamId) &&
            Equals(x.CommitId, y.CommitId);
    }

    public int GetHashCode(ICommit obj)
    {
        return HashCode.Combine(obj.BucketId, obj.StreamId, obj.CommitId);
    }
}