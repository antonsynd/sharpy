namespace Sharpy;

/// <summary>
/// An interface for a type that can be modulated (modulo operation) by something
/// producing a remainder.
/// </summary>
/// <typeparam name="TDivisor">The divisor (the thing to modulo by).</typeparam>
/// <typeparam name="TRemainder">The remainder (result).</typeparam>
public interface IModulableWith<TDivisor, TRemainder>
{
    TRemainder __Mod__(TDivisor other);
}

/// <summary>
/// An interface for a type (the dividend) that can be modulated by
/// something (the divisor) producing a remainder (the result).
/// This version has native operator support.
/// </summary>
/// <typeparam name="TDividend">The dividend (the thing to modulo).</typeparam>
/// <typeparam name="TDivisor">The divisor (the thing to modulo by).</typeparam>
/// <typeparam name="TRemainder">The remainder (result).</typeparam>
public interface IModulable<TDividend, TDivisor, TRemainder>
    : IModulableWith<TDivisor, TRemainder>
      where TDividend : IModulable<TDividend, TDivisor, TRemainder>
{
    static virtual TRemainder operator %(TDividend left, TDivisor right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("%", "NoneType");
        }

        return left.__Mod__(right);
    }
}

/// <summary>
/// An interface for a type that can be modulated by itself.
/// </summary>
/// <typeparam name="T">The type.</typeparam>
public interface IModulable<T>
    : IModulableWith<T, T>
      where T : IModulable<T>
{
    static virtual T operator %(T left, T right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("%", "NoneType");
        }

        return left.__Mod__(right);
    }
}
