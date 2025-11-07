namespace Sharpy;

/// <summary>
/// An interface for a type that can be subtracted from with something
/// producing a difference.
/// </summary>
/// <typeparam name="TSubtrahend">The subtrahend (the thing to subtract).</typeparam>
/// <typeparam name="TDifference">The difference (result).</typeparam>
public interface ISubtractableWith<TSubtrahend, TDifference>
{
    TDifference __Sub__(TSubtrahend other);
}

/// <summary>
/// An interface for a type (the minuend) that can be subtracted from
/// with something (the subtrahend) producing a difference (the result).
/// This version has native operator support.
/// </summary>
/// <typeparam name="TMinuend">The minuend (the thing to subtract from).</typeparam>
/// <typeparam name="TSubtrahend">The subtrahend (the thing to subtract).</typeparam>
/// <typeparam name="TDifference">The difference (result).</typeparam>
public interface ISubtractable<TMinuend, TSubtrahend, TDifference>
    : ISubtractableWith<TSubtrahend, TDifference>
      where TMinuend : ISubtractable<TMinuend, TSubtrahend, TDifference>
{
    static virtual TDifference operator -(TMinuend left, TSubtrahend right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("-", "NoneType");
        }

        return left.__Sub__(right);
    }
}

/// <summary>
/// An interface for a type that can be subtracted from itself.
/// </summary>
/// <typeparam name="T">The type.</typeparam>
public interface ISubtractable<T>
    : ISubtractableWith<T, T>
      where T : ISubtractable<T>
{
    static virtual T operator -(T left, T right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("-", "NoneType");
        }

        return left.__Sub__(right);
    }
}
