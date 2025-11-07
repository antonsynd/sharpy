namespace Sharpy;

/// <summary>
/// An interface for a type that can be raised to a power
/// producing a result.
/// </summary>
/// <typeparam name="TExponent">The exponent (the power to raise to).</typeparam>
/// <typeparam name="TResult">The result.</typeparam>
public interface IPowerableWith<TExponent, TResult>
{
    TResult __Pow__(TExponent other);
}

/// <summary>
/// An interface for a type (the base) that can be raised to a power
/// (the exponent) producing a result.
/// This version has native operator support via a custom method.
/// </summary>
/// <typeparam name="TBase">The base (the thing to raise to a power).</typeparam>
/// <typeparam name="TExponent">The exponent (the power to raise to).</typeparam>
/// <typeparam name="TResult">The result.</typeparam>
public interface IPowerable<TBase, TExponent, TResult>
    : IPowerableWith<TExponent, TResult>
      where TBase : IPowerable<TBase, TExponent, TResult>
{
    // Note: C# doesn't have a ** operator, so this cannot have a static operator overload
}

/// <summary>
/// An interface for a type that can be raised to a power of itself.
/// </summary>
/// <typeparam name="T">The type.</typeparam>
public interface IPowerable<T>
    : IPowerableWith<T, T>
      where T : IPowerable<T>
{
    // Note: C# doesn't have a ** operator, so this cannot have a static operator overload
}
