namespace Sharpy.Operator;

public static partial class Exports
{
    public static bool Le<T>(ILessThanOrEquatableWith<T> left, T right)
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        return left.__Le__(right);
    }

    public static bool Le<T>(IComparable<T> left, T right)
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        return left.CompareTo(right) < 0;
    }

    public static bool Le(IComparable left, object right)
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        return left.CompareTo(right) <= 0;
    }

    public static bool __Le__<T>(T left, T right) where T : ILessThanOrEquatable<T> => Le(left, right);

    public static bool __Le__<T>(IComparable<T> left, T right) => Le(left, right);

    public static bool __Le__<T>(IComparable left, object right) => Le(left, right);
}
