namespace Sharpy.Operator;

public static partial class Exports
{
    public static bool Lt<T>(T left, T right) where T : ILessThanComparable<T>
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        return left.__Lt__(right);
    }

    public static bool Lt<T>(IComparable<T> left, T right)
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

    public static bool Lt(IComparable left, object right)
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

    public static bool __Lt__<T>(T left, T right) where T : ILessThanComparable<T> => Lt(left, right);

    public static bool __Lt__<T>(IComparable<T> left, IComparable<T> right) => Lt(left, right);

    public static bool __Lt__<T>(IComparable left, IComparable right) => Lt(left, right);
}
