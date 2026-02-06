namespace Sharpy.Core.Tests;

using static Sharpy.Core.Builtins;

/// <summary>
/// Test helper wrapper class that provides Python-style dunder methods.
/// </summary>
public class Wrapper<T>(T value) : System.IEquatable<Wrapper<T>>
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

    // Hashable
    public int __Hash__()
    {
        var hashCode = new HashCode();
        hashCode.Add(typeof(Wrapper<T>).GetHashCode());
        hashCode.Add(Id.GetHashCode());
        hashCode.Add(Value?.GetHashCode());

        return hashCode.ToHashCode();
    }

    // Equatable
    public bool __Eq__(object? other)
    {
        if (other is Wrapper<T> wrapper)
        {
            return __Eq__(wrapper);
        }

        return false;
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
        if (obj is Wrapper<T> wrapper)
        {
            return Equals(wrapper);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return __Hash__();
    }
}
