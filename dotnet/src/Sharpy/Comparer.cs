namespace Sharpy
{
    /// <summary>
    /// Static helper that selects the best available KeyComparer given the
    /// available.
    /// </summary>
    internal static class Comparer<T>
    {
        public static readonly IComparer<T> Instance = CreateComparer();

        private static IComparer<T> CreateComparer()
        {
            if (typeof(Comparable<T>).IsAssignableFrom(typeof(T)) ||
                typeof(LessThanOrEquatable<T>).IsAssignableFrom(typeof(T)) ||
            (typeof(LessThanComparable<T>).IsAssignableFrom(typeof(T)) && typeof(Equatable<T>).IsAssignableFrom(typeof(T))))
            {
                return new LessThanOrEquatableComparer();
            }

            if (typeof(LessThanComparable<T>).IsAssignableFrom(typeof(T)))
            {
                return new LessThanComparableComparer();
            }

            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T))) {
                return new TypedIComparableComparer();
            }

            if (typeof(IComparable).IsAssignableFrom(typeof(T))) {
                return new UntypedIComparableComparer();
            }

            throw new TypeError("Provided type does not support comparison");
        }

        private class LessThanComparableComparer : IComparer<T>
        {
            public int Compare(T? x, T? y)
            {
                // These are the same objects
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                // x is less than y
                if (x is null)
                {
                    return -1;
                }

                // x is greater than y
                if (y is null)
                {
                    return 1;
                }

                var xlt = (LessThanComparable<T>)x;

                // x is less than y
                if (xlt.__Lt__(y))
                {
                    return -1;
                }

                var ylt = (LessThanComparable<T>)y;

                // y is less than y, so x is greater than y
                if (ylt.__Lt__(x))
                {
                    return 1;
                }

                // Neither y or x are less than each other, so both are equal
                return 0;
            }
        }

        private class LessThanOrEquatableComparer : IComparer<T>
        {
            public int Compare(T? x, T? y)
            {
                // These are the same objects
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                // x is less than y
                if (x is null)
                {
                    return -1;
                }

                // x is greater than y
                if (y is null)
                {
                    return 1;
                }

                var xeq = (Equatable<T>)x;
                var xlt = (LessThanComparable<T>)x;

                // Both are equal
                if (xeq.__Eq__(y))
                {
                    return 0;
                }

                // x is less than y
                if (xlt.__Lt__(y))
                {
                    return -1;
                }

                // x is not less than y and not equal to y, so it is greater
                // than y
                return 1;
            }
        }

        private class TypedIComparableComparer : IComparer<T>
        {
            public int Compare(T? x, T? y)
            {
                // These are the same objects
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                // x is less than y
                if (x is null)
                {
                    return -1;
                }

                // x is greater than y
                if (y is null)
                {
                    return 1;
                }

                return ((IComparable<T>)x).CompareTo(y);
            }
        }

        private class UntypedIComparableComparer : IComparer<T>
        {
            public int Compare(T? x, T? y)
            {
                // These are the same objects
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                // x is less than y
                if (x is null)
                {
                    return -1;
                }

                // x is greater than y
                if (y is null)
                {
                    return 1;
                }

                return ((IComparable)x).CompareTo(y);
            }
        }
    }
}
