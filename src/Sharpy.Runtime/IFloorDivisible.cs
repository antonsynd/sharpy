namespace Sharpy;

/// <summary>
/// An interface for a type that can be floor divided by something
/// producing a quotient.
/// </summary>
/// <typeparam name="TDivisor">The divisor (the thing to divide by).</typeparam>
/// <typeparam name="TQuotient">The quotient (result).</typeparam>
public interface IFloorDivisibleWith<TDivisor, TQuotient>
{
    TQuotient __FloorDiv__(TDivisor other);
}

/// <summary>
/// An interface for a type (the dividend) that can be floor divided by
/// something (the divisor) producing a quotient (the result).
/// Floor division returns the largest integer less than or equal to the quotient.
/// This version has native operator support via a custom method.
/// </summary>
/// <typeparam name="TDividend">The dividend (the thing to divide).</typeparam>
/// <typeparam name="TDivisor">The divisor (the thing to divide by).</typeparam>
/// <typeparam name="TQuotient">The quotient (result).</typeparam>
public interface IFloorDivisible<TDividend, TDivisor, TQuotient>
    : IFloorDivisibleWith<TDivisor, TQuotient>
      where TDividend : IFloorDivisible<TDividend, TDivisor, TQuotient>
{
    // Note: C# doesn't have a // operator, so this cannot have a static operator overload
}

/// <summary>
/// An interface for a type that can be floor divided by itself.
/// </summary>
/// <typeparam name="T">The type.</typeparam>
public interface IFloorDivisible<T>
    : IFloorDivisibleWith<T, T>
      where T : IFloorDivisible<T>
{
    // Note: C# doesn't have a // operator, so this cannot have a static operator overload
}
