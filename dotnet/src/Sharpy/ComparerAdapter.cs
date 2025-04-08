namespace Sharpy
{
    internal static class ComparerAdapter<T>
    {
        public static readonly IComparer<T> Instance = CreateComparer();

        private static IComparer<T> CreateComparer()
        {
            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)) ||
                typeof(ILessThanOrEquatable<T>).IsAssignableFrom(typeof(T)) ||
            (typeof(ILessThanComparable<T>).IsAssignableFrom(typeof(T)) && typeof(IEquatable<T>).IsAssignableFrom(typeof(T))))
            {
                return new LessThanOrEquatableComparer();
            }

            if (typeof(IGreaterThanOrEquatable<T>).IsAssignableFrom(typeof(T)) ||
            (typeof(IGreaterThanComparable<T>).IsAssignableFrom(typeof(T)) && typeof(IEquatable<T>).IsAssignableFrom(typeof(T))))
            {
                return new GreaterThanOrEquatableComparer();
            }

            if (typeof(ILessThanComparable<T>).IsAssignableFrom(typeof(T)))
            {
                return new LessThanComparableComparer();
            }

            if (typeof(IGreaterThanComparable<T>).IsAssignableFrom(typeof(T)))
            {
                return new GreaterThanComparableComparer();
            }

            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            {
                return new TypedIComparableComparer();
            }

            if (typeof(IComparable).IsAssignableFrom(typeof(T)))
            {
                return new UntypedIComparableComparer();
            }

            throw new TypeError($"'<' not supported for instances of ${typeof(T).Name}");
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
                if (x is null || y is null)
                {
                    throw new TypeError("'<' not supported for instances of 'NoneType'");
                }

                var xlt = (ILessThanComparable<T>)x;

                // x is less than y
                if (xlt.__Lt__(y))
                {
                    return -1;
                }

                var ylt = (ILessThanComparable<T>)y;

                // y is less than x, so x is greater than y
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
                if (x is null || y is null)
                {
                    throw new TypeError("'<' not supported for instances of 'NoneType'");
                }

                var xeq = (IEquatable<T>)x;
                var xlt = (ILessThanComparable<T>)x;

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

        private class GreaterThanComparableComparer : IComparer<T>
        {
            public int Compare(T? x, T? y)
            {
                // These are the same objects
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                if (x is null || y is null)
                {
                    throw new TypeError("'>' not supported for instances of 'NoneType'");
                }

                var xgt = (IGreaterThanComparable<T>)x;

                // x is greater than y
                if (xgt.__Gt__(y))
                {
                    return -1;
                }

                var ygt = (IGreaterThanComparable<T>)y;

                // y is greater than x, so x is less than y
                if (ygt.__Gt__(x))
                {
                    return -1;
                }

                // Neither y or x are greater than each other, so both are equal
                return 0;
            }
        }

        private class GreaterThanOrEquatableComparer : IComparer<T>
        {
            public int Compare(T? x, T? y)
            {
                // These are the same objects
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                if (x is null || y is null)
                {
                    throw new TypeError("'>' not supported for instances of 'NoneType'");
                }

                var xeq = (IEquatable<T>)x;
                var xlt = (IGreaterThanComparable<T>)x;

                // Both are equal
                if (xeq.__Eq__(y))
                {
                    return 0;
                }

                // x is greater than y
                if (xlt.__Gt__(y))
                {
                    return 1;
                }

                // x is not greater than y and not equal to y, so it is less
                // than y
                return -1;
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

                if (x is null || y is null)
                {
                    throw new TypeError("'<' not supported for instances of 'NoneType'");
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

                if (x is null || y is null)
                {
                    throw new TypeError("'<' not supported for instances of 'NoneType'");
                }

                return ((IComparable)x).CompareTo(y);
            }
        }
    }
}
