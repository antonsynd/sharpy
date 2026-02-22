namespace Sharpy.Core.Tests;

using static Sharpy.Builtins;

/// <summary>
/// Test helper wrapper class that provides Python-style dunder methods.
/// </summary>
public class Wrapper<T>(T value) : System.IEquatable<Wrapper<T>>, Sharpy.IBoolConvertible
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
    public int GetId()
    {
        return (int)Id;
    }

    // IBoolConvertible
    public bool IsTrue => Bool(Value);

    public static bool operator true(Wrapper<T> wrapper)
    {
        return wrapper?.IsTrue ?? false;
    }

    public static bool operator false(Wrapper<T> wrapper)
    {
        return !(wrapper?.IsTrue ?? false);
    }

    // Representable
    public string Repr()
    {
        return $"<Wrapper object with id {Id} and value {Sharpy.Builtins.Repr(Value)}>";
    }

    // Hashable
    public int Hash()
    {
        var hashCode = new HashCode();
        hashCode.Add(typeof(Wrapper<T>).GetHashCode());
        hashCode.Add(Id.GetHashCode());
        hashCode.Add(Value?.GetHashCode());

        return hashCode.ToHashCode();
    }

    // Equatable<Wrapper<T>>
    public bool Equals(Wrapper<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        return Value?.Equals(other.Value) ?? false;
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
    public bool Ne(Wrapper<T> other)
    {
        return !Equals(other);
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
        return Hash();
    }
}
