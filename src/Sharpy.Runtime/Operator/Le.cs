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

    public static bool Le<T>(T left, T right)
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        if (typeof(T).IsAssignableTo(typeof(ILessThanOrEquatableWith<T>)))
        {
            return Le((ILessThanOrEquatableWith<T>)left, right);
        }

        if (typeof(T).IsAssignableTo(typeof(IComparable<T>)))
        {
            return Le((IComparable<T>)left, right);
        }

        if (typeof(T).IsAssignableTo(typeof(IComparable)))
        {
            return Le((IComparable)left, right);
        }

        throw TypeError.OpNotSupported("<", typeof(T).Name);
    }

    public static bool __Le__<T>(ILessThanOrEquatableWith<T> left, T right) => Le(left, right);

    public static bool __Le__<T>(IComparable<T> left, T right) => Le(left, right);

    public static bool __Le__<T>(IComparable left, object right) => Le(left, right);

    public static bool __Le__<T>(T left, T right) => Le(left, right);
}
