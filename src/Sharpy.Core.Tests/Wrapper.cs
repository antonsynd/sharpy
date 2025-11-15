namespace Sharpy.Core.Tests;

public class Wrapper<T>(T value) : object, Sharpy.Core.IEquatable<Wrapper<T>>, Sharpy.Core.IInequatable<Wrapper<T>>, Sharpy.Core.IEquatableWith<object>, Sharpy.Core.IInequatableWith<object>, Sharpy.Core.IBoolConvertible, Sharpy.Core.IRepresentable, Sharpy.Core.IHashable, Sharpy.Core.IIdentifiable, Sharpy.Core.IStrConvertible
{
    private static uint _id;

    public readonly uint Id = _id++;

    public readonly T Value = value;

    public static implicit operator Wrapper<T>(T value)
    {
        return new Wrapper<T>(value);
    }

    public static void ResetId()
    {
        _id = 0;
    }

    // Identifiable
    public int __Id__()
    {
        return (int)Id;
    }

    // BoolConvertible
    public bool __Bool__()
    {
        return Bool(Value);
    }

    public static bool operator true(Wrapper<T> wrapper)
    {
        return wrapper?.__Bool__() ?? false;
    }

    public static bool operator false(Wrapper<T> wrapper)
    {
        return !(wrapper?.__Bool__() ?? false);
    }

    // Representable
    public string __Repr__()
    {
        return $"<Wrapper object with id {Id} and value {Repr(Value)}>";
    }

    // StrConvertible
    public string __Str__()
    {
        return __Repr__();
    }

    // Hashable
    public int __Hash__()
    {
        var hashCode = new HashCode();
        hashCode.Add(typeof(Wrapper<T>).GetHashCode());
        hashCode.Add(Id.GetHashCode());
        hashCode.Add(Value?.GetHashCode());

        return hashCode.ToHashCode();
    }

    // Equatable<object>
    public bool __Eq__(object other)
    {
        if (other is Wrapper<T> wrapper)
        {
            return __Eq__(wrapper);
        }

        return false;
    }

    // Inequatable<object>
    public bool __Ne__(object other)
    {
        return !__Eq__(other);
    }

    // Equatable<Wrapper<T>>
    public bool __Eq__(Wrapper<T> other)
    {
        if (other is null)
        {
            return false;
        }

        return Value?.Equals(other.Value) ?? false;
    }

    public bool Equals(Wrapper<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        return __Eq__(other);
    }

    public static bool operator ==(Wrapper<T> left, Wrapper<T> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return Equals(left.Value, right.Value);
    }

    public static bool operator !=(Wrapper<T> left, Wrapper<T> right)
    {
        return !(left == right);
    }

    // Inequatable<Wrapper<T>>
    public bool __Ne__(Wrapper<T> other)
    {
        return !__Eq__(other);
    }

    public override bool Equals(object? obj)
    {
        return __Eq__(obj);
    }

    public override int GetHashCode()
    {
        return __Hash__();
    }

    public override string ToString()
    {
        return __Str__();
    }
}
