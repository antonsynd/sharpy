namespace Sharpy;

public interface IGreaterThanComparableWith<T>
{
    bool __Gt__(T other);
}

public interface IGreaterThanComparable<T> : IGreaterThanComparableWith<T> where T : IGreaterThanComparable<T>
{
    static virtual bool operator >(T left, T right)
    {
        if (left is null || right is null)
        {
            throw new TypeError("'<' is not supported for objects of 'NoneType'");
        }

        return left.__Gt__(right);
    }

    static abstract bool operator >=(T left, T right);

    static abstract bool operator <(T left, T right);

    static virtual bool operator <=(T left, T right)
    {
        return !(left > right);
    }
}
