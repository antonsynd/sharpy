namespace Sharpy
{
    /// <summary>
    /// Static helper that selects the best available KeyComparer given the
    /// available.
    /// </summary>
    file static class KeyComparer<TKey>
    {
        public static readonly IComparer<TKey> Instance = CreateComparer();

        private static IComparer<TKey> CreateComparer()
        {
            if (typeof(LessThanComparable<TKey>).IsAssignableFrom(typeof(TKey))) {
                return new LessThanComparableComparer();
            }

            if (typeof(LessThanOrEquatable<TKey>).IsAssignableFrom(typeof(TKey))) {
                return new LessThanOrEquatableComparer();
            }

            if (typeof(IComparable<TKey>).IsAssignableFrom(typeof(TKey))) {
                return Comparer<TKey>.Default;
            }

            if (typeof(IComparable).IsAssignableFrom(typeof(TKey))) {
                return Comparer<TKey>.Default;
            }

            return new DefaultComparer();
        }

        private class LessThanComparableComparer : IComparer<TKey>
        {
            public int Compare(TKey? x, TKey? y)
            {
                // These are the same objects
                if (ReferenceEquals(x, y)) {
                    return 0;
                }

                // x is less than y
                if (x is null) {
                    return -1;
                }

                // x is greater than y
                if (y is null) {
                    return 1;
                }

                var lt = (LessThanComparable<TKey>)x;
                if (lt.__Lt__(y)) return -1;

                var ylt = y as LessThanComparable<TKey>;
                if (ylt != null && ylt.__Lt__(x)) return 1;

                return 0;
            }
        }

        private class LessThanOrEquatableComparer : IComparer<TKey>
        {
            public int Compare(TKey? x, TKey? y)
            {
                // These are the same objects
                if (ReferenceEquals(x, y)) {
                    return 0;
                };

                // x is less than y
                if (x is null) {
                    return -1;
                }

                // x is greater than y
                if (y is null) {
                    return 1;
                }

                var xle = (LessThanOrEquatable<TKey>)x;
                var yle = (LessThanOrEquatable<TKey>)y;

                // Both are equal
                if (xle.__Eq__(y)) {
                    return 0;
                }

                // x is less than y or the opposite
                if (xle.__Le__(y)) {
                    return -1;
                }

                return 1;
            }
        }

        private class DefaultComparer : IComparer<TKey>
        {
            public int Compare(TKey? x, TKey? y)
            {
                return Comparer<TKey>.Default.Compare(x, y);
            }
        }
    }

    /// <summary>
    /// Delegator to statically chosen comparer.
    /// </summary>
    file class KeyComparer<T, TKey>(Func<T?, TKey?> key) : IComparer<T>
    {
        private readonly Func<T?, TKey?> _key = key;

        /// <remarks>
        /// Unlike in Python, this compares None to non-None values as ordering
        /// before the non-None value, whereas Python disallows such comparisons
        /// by raising a TypeError.
        /// </remarks>
        public int Compare(T? x, T? y)
        {
            return KeyComparer<TKey>.Instance.Compare(_key(x), _key(y));
        }

    }

    /// <summary>
    /// Simplifies creation of a key comparer if the key is null, by returning
    /// the comparer for T rather than for TKey.
    /// </summary>
    internal class KeyComparerFactory<T, TKey>
    {
        public static IComparer<T> Create(Func<T?, TKey?>? key = null)
        {
            // If the key selector is null
            if (key == null)
            {
                return KeyComparer<T>.Instance;
            }

            return new KeyComparer<T, TKey>(key);
        }
    }
}
