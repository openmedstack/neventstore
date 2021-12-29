using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OpenMedStack.NEventStore
{
    internal class ImmutableCollection<T> : ICollection<T>, ICollection
    {
        private readonly ICollection<T> _inner;

        public ImmutableCollection(ICollection<T> inner)
        {
            _inner = inner;
        }

        public virtual object SyncRoot { get; } = new object();

        public virtual bool IsSynchronized => false;

        public virtual void CopyTo(Array array, int index)
        {
            CopyTo(array.Cast<T>().ToArray(), index);
        }

        public virtual int Count => _inner.Count;

        public virtual bool IsReadOnly => true;

        public virtual IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public virtual void Add(T item)
        {
            throw new NotSupportedException(Resources.ReadOnlyCollection);
        }

        public virtual bool Remove(T item) => throw new NotSupportedException(Resources.ReadOnlyCollection);

        public virtual void Clear()
        {
            throw new NotSupportedException(Resources.ReadOnlyCollection);
        }

        public virtual bool Contains(T item) => _inner.Contains(item);

        public virtual void CopyTo(T[] array, int arrayIndex)
        {
            _inner.CopyTo(array, arrayIndex);
        }
    }
}