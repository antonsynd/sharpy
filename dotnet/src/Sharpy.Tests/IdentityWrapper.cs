namespace Sharpy.Tests;

public sealed class IdentityWrapper<T> : Wrapper<T>
{
    public IdentityWrapper(T value) : base(value)
    {
    }

    public override bool __Eq__(Object other)
    {
        if (other is IdentityWrapper<T> wrapper)
        {
            return Id == wrapper.Id;
        }

        return false;
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

    public override bool __Bool__()
    {
        return Bool(Value);
    }
}
