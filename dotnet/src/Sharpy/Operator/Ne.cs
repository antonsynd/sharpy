namespace Sharpy.Operator;

public static partial class Exports
{
    public static bool Ne<T>(T left, T right) where T : IInequatable<T>
    {
        if (ReferenceEquals(left, right)) {
            return false;
        }

        return left?.__Ne__(right) ?? !(right is null);
    }

    public static bool Ne(Object left, Object right)
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        return !left?.__Eq__(right) ?? !(right is null);
    }

    public static bool Ne<T>(IComparable<T> left, T right)
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        return left?.CompareTo(right) != 0;
    }

    public static bool Ne(IComparable left, object right)
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        return left?.CompareTo(right) != 0;
    }

    public static bool Ne(object left, object right)
    {
        return !(left?.Equals(right) ?? right is null);
    }

    public static bool __Ne__<T>(T left, T right) where T : IInequatable<T> => Ne(left, right);

    public static bool __Ne__(Object left, Object right) => Eq(left, right);

    public static bool __Ne__<T>(IComparable<T> left, T right) => Eq(left, right);

    public static bool __Ne__(IComparable left, object right) => Eq(left, right);

    public static bool __Ne__(object left, object right) => Ne(left, right);
}
