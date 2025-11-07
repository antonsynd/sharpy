namespace Sharpy;

internal static class IdentityAdapterFactory<T>
{
    public static readonly Func<T, T, bool> AreSame = GetAdapter();

    private static Func<T, T, bool> GetAdapter()
    {
        // Prefer __Id__()
        if (typeof(T).IsSubclassOf(typeof(Object)))
        {
            return IdentityAdapter.AreSame;
        }

        if (typeof(T).IsValueType)
        {
            return EqualsAdapter.AreSame;
        }

        // By default, use ReferenceEquals()
        return ReferenceEqualsAdapter.AreSame;
    }

    private static class IdentityAdapter
    {
        public static bool AreSame(T lhs, T rhs)
        {
            var lhsObject = lhs as Object;
            var rhsObject = rhs as Object;

            if (lhsObject is null)
            {
                if (rhsObject is null)
                {
                    return true;
                }

                return false;
            }

            if (rhsObject is null)
            {
                return false;
            }

            return lhsObject.__Id__() == rhsObject.__Id__();
        }
    }

    private static class EqualsAdapter
    {
        public static bool AreSame(T lhs, T rhs)
        {
            if (lhs is null)
            {
                return false;
            }

            return lhs.Equals(rhs);
        }
    }

    private static class ReferenceEqualsAdapter
    {
        public static bool AreSame(T lhs, T rhs)
        {
            return ReferenceEquals(lhs, rhs);
        }
    }
}
