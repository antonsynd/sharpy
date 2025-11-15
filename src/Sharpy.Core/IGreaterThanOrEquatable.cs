namespace Sharpy.Core;

public interface IGreaterThanOrEquatableWith<T> : IGreaterThanComparableWith<T>, IEquatableWith<T>
{
    bool __Ge__(T other);
}

public interface IGreaterThanOrEquatable<T> : IGreaterThanOrEquatableWith<T>, IGreaterThanComparable<T>, IEquatable<T> where T : IGreaterThanOrEquatable<T>
{
    static virtual bool operator >=(T left, T right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported(">", "NoneType");
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
            throw TypeError.OpNotSupported(">", "NoneType");
        }

        return left.__Gt__(right);
    }
}
