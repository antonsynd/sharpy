namespace Sharpy;

public interface ILessThanOrEquatableWith<T> : ILessThanComparableWith<T>
{
    bool __Le__(T other);
}

public interface ILessThanOrEquatable<T> : ILessThanOrEquatableWith<T>, ILessThanComparable<T>, IEquatable<T> where T : ILessThanOrEquatable<T>
{
    static virtual bool operator <=(T left, T right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        return left.__Le__(right);
    }

    static virtual bool operator >(T left, T right)
    {
        return !(left <= right);
    }

    static virtual bool operator <(T left, T right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
        }

        return left.__Lt__(right);
    }

    static virtual bool operator >=(T left, T right)
    {
        return !(left < right);
    }
}
