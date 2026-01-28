namespace Sharpy.Core;

internal static class ComparerAdapter<T>
{
    public static readonly IComparer<T> Instance = CreateComparer();

    private static IComparer<T> CreateComparer()
    {
        if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
        {
            return new TypedIComparableComparer();
        }

        if (typeof(IComparable).IsAssignableFrom(typeof(T)))
        {
            return new UntypedIComparableComparer();
        }

        throw TypeError.OpNotSupported("<", typeof(T).Name);
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
                throw TypeError.OpNotSupported("<", "NoneType");
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
                throw TypeError.OpNotSupported("<", "NoneType");
            }

            return ((IComparable)x).CompareTo(y);
        }
    }
}
