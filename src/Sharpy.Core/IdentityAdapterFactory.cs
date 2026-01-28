namespace Sharpy.Core;

internal static class IdentityAdapterFactory<T>
{
    public static readonly Func<T, T, bool> AreSame = GetAdapter();

    private static Func<T, T, bool> GetAdapter()
    {
        if (typeof(T).IsValueType)
        {
            return EqualsAdapter.AreSame;
        }

        // By default, use ReferenceEquals()
        return ReferenceEqualsAdapter.AreSame;
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
