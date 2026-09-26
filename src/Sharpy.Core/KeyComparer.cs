using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Delegator to statically chosen comparer.
    /// </summary>
    internal class KeyComparer<T, TKey> : IComparer<T>
    {
        private readonly Func<T, TKey> _key;

        public KeyComparer(Func<T, TKey> key)
        {
            _key = key;
        }

        /// <remarks>
        /// Python compares the KEYS, never the elements: a None element is passed to the key
        /// (<c>sorted([None, 1], key=lambda v: 0)</c> is <c>[None, 1]</c>), and only a key comparison
        /// can be refused (#2085).
        /// </remarks>
        public int Compare(T? x, T? y)
        {
            return ComparerAdapter<TKey>.Instance.Compare(_key(x!), _key(y!));
        }

    }

    /// <summary>
    /// Simplifies creation of a key comparer if the key is null, by returning
    /// the comparer for T rather than for TKey.
    /// </summary>
    internal class KeyComparerFactory<T, TKey>
    {
        public static IComparer<T> Create(Func<T, TKey> key)
        {
            return new KeyComparer<T, TKey>(key);
        }
    }
}
