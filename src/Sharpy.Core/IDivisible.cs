namespace Sharpy.Core;

/// <summary>
/// An interface for a type that can be divided by something
/// producing a quotient.
/// </summary>
/// <typeparam name="TDivisor">The divisor (the thing to divide by).</typeparam>
/// <typeparam name="TQuotient">The quotient (result).</typeparam>
public interface IDivisibleWith<TDivisor, TQuotient>
{
    TQuotient __TrueDiv__(TDivisor other);
}

/// <summary>
/// An interface for a type (the dividend) that can be divided by
/// something (the divisor) producing a quotient (the result).
/// This version has native operator support.
/// </summary>
/// <typeparam name="TDividend">The dividend (the thing to divide).</typeparam>
/// <typeparam name="TDivisor">The divisor (the thing to divide by).</typeparam>
/// <typeparam name="TQuotient">The quotient (result).</typeparam>
public interface IDivisible<TDividend, TDivisor, TQuotient>
    : IDivisibleWith<TDivisor, TQuotient>
      where TDividend : IDivisible<TDividend, TDivisor, TQuotient>
{
    static virtual TQuotient operator /(TDividend left, TDivisor right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("/", "NoneType");
        }

        return left.__TrueDiv__(right);
    }
}

/// <summary>
/// An interface for a type that can be divided by itself.
/// </summary>
/// <typeparam name="T">The type.</typeparam>
public interface IDivisible<T>
    : IDivisibleWith<T, T>
      where T : IDivisible<T>
{
    static virtual T operator /(T left, T right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("/", "NoneType");
        }

        return left.__TrueDiv__(right);
    }
}
