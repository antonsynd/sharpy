namespace Sharpy.Operator;

public static partial class Exports
{
    public static bool Gt<T>(T left, T right) where T : IGreaterThanComparable<T>
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        return left.__Gt__(right);
    }

    public static bool Gt<T>(IComparable<T> left, T right)
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        return left.CompareTo(right) > 0;
    }

    public static bool Gt(IComparable left, object right)
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        return left.CompareTo(right) > 0;
    }

    public static bool __Gt__<T>(T left, T right) where T : IGreaterThanComparable<T> => Gt(left, right);

    public static bool __Gt__<T>(IComparable<T> left, T right) => Gt(left, right);

    public static bool __Gt__<T>(IComparable left, object right) => Gt(left, right);
}
