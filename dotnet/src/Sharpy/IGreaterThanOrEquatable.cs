namespace Sharpy;

public interface IGreaterThanOrEquatable<T> : IGreaterThanComparable<T>, IEquatable<T> where T : IGreaterThanOrEquatable<T>
{
    bool __Ge__(T other);

    static virtual bool operator >=(T left, T right)
    {
        if (left is null || right is null)
        {
            throw new TypeError("'<' is not supported for objects of 'NoneType'");
        }

        return left.__Ge__(right);
    }

    static virtual bool operator <(T left, T right)
    {
        return !(left >= right);
    }

    static virtual bool operator <=(T left, T right)
    {
        return !(left > right);
    }

    static virtual bool operator >(T left, T right)
    {
        if (left is null || right is null)
        {
            throw new TypeError("'<' is not supported for objects of 'NoneType'");
        }

        return left.__Gt__(right);
    }
}
