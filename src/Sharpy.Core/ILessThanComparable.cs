namespace Sharpy.Core;

public interface ILessThanComparableWith<T>
{
    bool __Lt__(T other);
}

public interface ILessThanComparable<T> : ILessThanComparableWith<T> where T : ILessThanComparable<T>
{
    static virtual bool operator <(T left, T right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
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
