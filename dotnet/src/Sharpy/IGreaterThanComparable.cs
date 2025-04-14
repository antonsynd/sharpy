namespace Sharpy;

public interface IGreaterThanComparable<T> where T : IGreaterThanComparable<T>
{
    bool __Gt__(T other);

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
