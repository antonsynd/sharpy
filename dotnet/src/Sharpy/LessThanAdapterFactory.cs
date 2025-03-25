namespace Sharpy
{
    internal static class LessThanAdapterFactory<T>
    {
        public static readonly Func<T?, T?, bool> IsLessThan = GetAdapter();

        private static Func<T?, T?, bool> GetAdapter()
        {
            // Prefer __Lt__(). __Gt__() is never used for equality or
            // sorting by design.
            if (typeof(T).IsAssignableTo(typeof(LessThanComparable<T>)))
            {
                return LessThanComparableAdapter.IsLessThan;
            }

            if (typeof(T).IsAssignableTo(typeof(IComparable<T>)))
            {
                return TypedIComparableAdapter.IsLessThan;
            }

            if (typeof(T).IsAssignableTo(typeof(IComparable)))
            {
                return UntypedIComparableAdapter.IsLessThan;
            }

            throw new TypeError($"'<' not supported for instances of ${typeof(T).Name}");
        }

        private static class LessThanComparableAdapter
        {

            public static bool IsLessThan(T? lhs, T? rhs)
            {
                // References to the same object are always not less than
                if (ReferenceEquals(lhs, rhs))
                {
                    return false;
                }

                if (lhs is null || rhs is null)
                {
                    throw new TypeError("'<' not supported for instances of 'NoneType'");
                }

                return (lhs as LessThanComparable<T>)?.__Lt__(rhs) ?? false;
            }
        }

        private static class TypedIComparableAdapter
        {
            public static bool IsLessThan(T? lhs, T? rhs)
            {
                // References to the same object are always not less than
                if (ReferenceEquals(lhs, rhs))
                {
                    return false;
                }

                if (lhs is null || rhs is null)
                {
                    throw new TypeError("'<' not supported for instances of 'NoneType'");
                }

                return ((lhs as IComparable<T>)?.CompareTo(rhs) ?? 0) < 0;
            }
        }

        private static class UntypedIComparableAdapter
        {
            public static bool IsLessThan(T? lhs, T? rhs)
            {
                // References to the same object are always not less than
                if (ReferenceEquals(lhs, rhs))
                {
                    return false;
                }

                if (lhs is null || rhs is null)
                {
                    throw new TypeError("'<' not supported for instances of 'NoneType'");
                }

                return ((lhs as IComparable)?.CompareTo(rhs) ?? 0) < 0;
            }
        }
    }
}
