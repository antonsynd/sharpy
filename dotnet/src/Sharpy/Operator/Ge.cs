namespace Sharpy.Operator;

public static partial class Exports
{
    public static bool Ge<T>(IGreaterThanOrEquatableWith<T> left, T right)
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        return left.__Ge__(right);
    }

    public static bool Ge<T>(IComparable<T> left, T right)
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        return left.CompareTo(right) >= 0;
    }

    public static bool Ge(IComparable left, object right)
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        return left.CompareTo(right) >= 0;
    }

    public static bool __Ge__<T>(T left, T right) where T : IGreaterThanOrEquatable<T> => Ge(left, right);

    public static bool __Ge__<T>(IComparable<T> left, T right) => Ge(left, right);

    public static bool __Ge__<T>(IComparable left, object right) => Ge(left, right);
}
