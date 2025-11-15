namespace Sharpy.Core;

public interface IInequatableWith<T> : IEquatableWith<T>
{
    bool __Ne__(T other)
    {
        return ((IEquatableWith<T>)this)?.__Eq__(other) ?? other is null;
    }

    bool __Ne__(object other)
    {
        if (other is T tOther)
        {
            return __Ne__(tOther);
        }

        return false;
    }
}

public interface IInequatable<T> : IInequatableWith<T>, IEquatable<T> where T : IEquatable<T>, IInequatable<T>
{
    static virtual bool operator ==(T left, T right)
    {
        return ((IEquatable<T>)left)?.__Eq__(right) ?? right is null;
    }

    static virtual bool operator !=(T left, T right)
    {
        return left?.__Ne__(right) ?? !(right is null);
    }

    static virtual bool operator ==(T left, object right)
    {
        if (right is T tRight)
        {
            return left == tRight;
        }

        return false;
    }

    static virtual bool operator !=(T left, object right)
    {
        return !(left == right);
    }

    static virtual bool operator ==(object left, T right)
    {
        if (left is T tLeft)
        {
            return tLeft == right;
        }

        return false;
    }

    static virtual bool operator !=(object left, T right)
    {
        return !(left == right);
    }
}
