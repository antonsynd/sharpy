using Sharpy.Core;
namespace Sharpy.Operator;

public static partial class Exports
{
    public static bool Lt<T>(ILessThanComparableWith<T> left, T right)
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

    public static bool Lt<T>(T left, T right)
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        if (typeof(T).IsAssignableTo(typeof(ILessThanComparableWith<T>)))
        {
            return Lt((ILessThanComparableWith<T>)left, right);
        }

        if (typeof(T).IsAssignableTo(typeof(IComparable<T>)))
        {
            return Lt((IComparable<T>)left, right);
        }

        if (typeof(T).IsAssignableTo(typeof(IComparable)))
        {
            return Lt((IComparable)left, right);
        }

        throw TypeError.OpNotSupported("<", typeof(T).Name);
    }

    public static bool __Lt__<T>(ILessThanComparableWith<T> left, T right) => Lt(left, right);

    public static bool __Lt__<T>(IComparable<T> left, T right) => Lt(left, right);

    public static bool __Lt__<T>(IComparable left, object right) => Lt(left, right);

    public static bool __Lt__<T>(T left, T right) => Lt(left, right);
}
