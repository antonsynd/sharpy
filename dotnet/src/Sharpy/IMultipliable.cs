namespace Sharpy;

/// <summary>
/// An interface for a type (the multiplicand) that can be multiplied with
/// something (the multiplier) producing a result (the product).
/// </summary>
/// <typeparam name="TMultiplier">The multiplier (the factor).</typeparam>
/// <typeparam name="TProduct">The product.</typeparam>
public interface IMultipliableWith<TMultiplier, TProduct>
{
    TProduct __Mul__(TMultiplier other);
}

/// <summary>
/// An interface for a type (the multiplicand) that can be multiplied with
/// something (the multiplier) producing a result (the product) with native
/// operator support.
/// </summary>
/// <typeparam name="TMultiplicand">The multiplicand (what is being
/// multiplied).</typeparam>
/// <typeparam name="TMultiplier">The multiplier (i.e. the factor).</typeparam>
/// <typeparam name="TProduct">The product.</typeparam>
public interface IMultipliable<TMultiplicand, TMultiplier, TProduct> : IMultipliableWith<TMultiplier, TProduct>
    where TMultiplicand : IMultipliable<TMultiplicand, TMultiplier, TProduct>
{
    static virtual TProduct operator *(TMultiplicand left, TMultiplier right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("*", "NoneType");
        }

        return left.__Mul__(right);
    }
}

/// <summary>
/// An interface for a type (the multiplicand) that can be multiplied with
/// something (the multiplier) producing a result (the product) with native
/// operator support, where the result is of the same type as the multiplicand.
/// </summary>
/// <typeparam name="TMultiplicannd">The multiplicand (what is being
/// multiplied), and also the type of the product (the result).</typeparam>
/// <typeparam name="TMultiplier">The multiplier (i.e. the factor).</typeparam>
public interface IMultipliable<TMultiplicand, TMultipilier> : IMultipliable<TMultiplicand, TMultipilier, TMultiplicand>
    where TMultiplicand : IMultipliable<TMultiplicand, TMultipilier, TMultiplicand>
{
}

/// <summary>
/// An interface for a type that can be multiplied by itself yielding a result
/// of the same type.
/// </summary>
public interface IMultipliable<T> : IMultipliable<T, T, T> where T : IMultipliable<T, T, T>
{
}
