namespace Sharpy;

public interface ILessThanComparable<T> where T : ILessThanComparable<T>
{
    bool __Lt__(T other);

    static virtual bool operator <(T left, T right)
    {
        if (left is null || right is null)
        {
            throw new TypeError("'<' is not supported for objects of 'NoneType'");
        }

        return left.__Lt__(right);
    }

    static abstract bool operator <=(T left, T right);

    static abstract bool operator >(T left, T right);

    static virtual bool operator >=(T left, T right)
    {
        return !(left < right);
    }
}
