namespace Sharpy;

public interface ILessThanOrEquatable<T> : ILessThanComparable<T>, IEquatable<T> where T : ILessThanOrEquatable<T>
{
    bool __Le__(T other);

    static virtual bool operator <=(T left, T right)
    {
        if (left is null || right is null)
        {
            throw new TypeError("'<' is not supported for objects of 'NoneType'");
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
            throw new TypeError("'<' is not supported for objects of 'NoneType'");
        }

        return left.__Lt__(right);
    }

    static virtual bool operator >=(T left, T right)
    {
        return !(left < right);
    }
}
