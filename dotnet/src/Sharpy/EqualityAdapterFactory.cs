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

            // By default, use Equals() (which for Sharpy Objects will
            // delegate to __Eq__() anyway due to it being sealed in Object).
            return EqualsAdapter.AreEqual;
        }

        private static class EquatableAdapter
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

                return ((Equatable<T>)lhs).__Eq__(rhs);
            }
        }

        private static class LessThanComparableAdapter
        {
            public static bool AreEqual(T lhs, T rhs)
            {
                if (lhs == null || rhs == null)
                {
                    throw new TypeError($"'<' not supported between instances of None and ${typeof(T).Name}");
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
    }
}
