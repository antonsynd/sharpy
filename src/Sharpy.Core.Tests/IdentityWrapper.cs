namespace Sharpy.Core.Tests;

using static Sharpy.Core.Builtins;

public sealed class IdentityWrapper<T> : Wrapper<T>
{
    public IdentityWrapper(T value) : base(value)
    {
    }

    public bool __Eq__(IdentityWrapper<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        return Id == other.Id;
    }

    public static implicit operator IdentityWrapper<T>(T value)
    {
        return new IdentityWrapper<T>(value);
    }

    public static bool operator ==(IdentityWrapper<T> left, IdentityWrapper<T> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Id == right.Id;
    }

    public static bool operator !=(IdentityWrapper<T> left, IdentityWrapper<T> right)
    {
        return !(left == right);
    }

    public override bool Equals(object? obj)
    {
        if (obj is IdentityWrapper<T> wrapper)
        {
            return Id == wrapper.Id;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return __Hash__();
    }
}
