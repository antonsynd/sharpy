namespace Sharpy
{
    internal static class EqualityAdapterFactory<T>
    {
        public static readonly Func<T, T, bool> AreEqual = GetAdapter();

        private static Func<T, T, bool> GetAdapter()
        {
            // Prefer __Eq__()
            if (typeof(T).IsAssignableTo(typeof(Equatable<T>)))
            {
                return EquatableAdapter.AreEqual;
            }

            // Otherwise use __Lt__(). __Gt__() is never used for equality
            // or sorting by design.
            if (typeof(T).IsAssignableTo(typeof(LessThanComparable<T>)))
            {
                return LessThanComparableAdapter.AreEqual;
            }

            if (typeof(T).IsValueType) {
                return EqualsAdapter.AreEqual;
            }

            return ReferenceEqualsAdapter.AreEqual;
        }

        private static class EquatableAdapter
        {

            public static bool AreEqual(T lhs, T rhs)
            {
                // References to the same object are always equal
                if (ReferenceEquals(lhs, rhs)) {
                    return true;
                }

                return (lhs as Equatable<T>)?.__Eq__(rhs) ?? false;
            }
        }

        private static class LessThanComparableAdapter
        {
            public static bool AreEqual(T lhs, T rhs)
            {
                // References to the same object are always equal
                if (ReferenceEquals(lhs, rhs)) {
                    return true;
                }

                // If the above doesn't establish equality, then if either of
                // them are false, then return false because None with any
                // other type is false.
                if (lhs == null || rhs == null)
                {
                    return false;
                }

                // Neither is less than the other, so they are equal
                return !((LessThanComparable<T>)lhs).__Lt__(rhs) && !((LessThanComparable<T>)rhs).__Lt__(lhs);
            }
        }

        private static class EqualsAdapter
        {
            public static bool AreEqual(T lhs, T rhs)
            {
                if (lhs == null)
                {
                    if (rhs == null)
                    {
                        return true;
                    }

                    return false;
                }

                return lhs.Equals(rhs);
            }
        }

        private static class ReferenceEqualsAdapter
        {
            public static bool AreEqual(T lhs, T rhs)
            {
                // References to the same object are always equal
                if (ReferenceEquals(lhs, rhs)) {
                    return true;
                }

                return lhs?.Equals(rhs) ?? false;
            }
        }
    }
}
