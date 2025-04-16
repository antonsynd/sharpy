namespace Sharpy;

/// <summary>
/// This interface defines objects that can be checked for equality via
/// the <c>__Eq__</c> method. Equality in Sharpy is not the same as
/// reference equality (except for <see cref="Object"/>). All Sharpy
/// objects implement this interface. This is the non-generic version of
/// the <see cref="IEquatable&lt;T&gt;"/> interface.
/// </summary>
public interface IEquatable : IHashable
{
    /// <remarks>
    /// This should delegate to <see cref="__Eq__(object)"/>. All C# (and
    /// therefore Sharpy) objects declare this by existing because they
    /// all subclass <see cref="object"/> which implements this.
    /// </remarks>
    bool Equals(object? other)
    {
        if (other is null)
        {
            return false;
        }

        return __Eq__(other);
    }

    /// <remarks>
    /// This should delegate to <see cref="IEquatable&lt;T&gt;.__Eq__(T)"/>
    /// when possible.
    /// </remarks>
    bool __Eq__(object other);
}

public interface IEquatableWith<T> : IEquatable, System.IEquatable<T>
{
    /// <remarks>
    /// This defines equality between two objects of the same type.
    /// </remarks>
    bool __Eq__(T other);

    new bool __Eq__(object other)
    {
        if (other is T obj)
        {
            return __Eq__(obj);
        }

        // NOTE: Do NOT call Equals() because it will result in an infinite
        // loop as Equals() ultimately references __Eq__()
        return ReferenceEquals(this, other);
    }
}

/// <summary>
/// This interface defines objects that can be checked for equality via
/// the <c>__Eq__</c> method. All Sharpy objects implement this interface.
/// This is the generic version of the <see cref="IEquatable"/> interface.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IEquatable<T> : IEquatableWith<T> where T : IEquatable<T>
{
    static virtual bool operator ==(T left, T right)
    {
        return left?.__Eq__(right) ?? right is null;
    }

    static virtual bool operator !=(T left, T right)
    {
        return !(left == right);
    }

    static virtual bool operator ==(T left, object right)
    {
        return left?.__Eq__(right) ?? right is null;
    }

    static virtual bool operator !=(T left, object right)
    {
        return !(left == right);
    }

    static virtual bool operator ==(object left, T right)
    {
        return right?.__Eq__(right) ?? left is null;
    }

    static virtual bool operator !=(object left, T right)
    {
        return !(left == right);
    }
}
