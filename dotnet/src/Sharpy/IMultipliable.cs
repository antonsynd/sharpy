namespace Sharpy;

public interface IMultipliable<TMultiplicand, TMultiplier, TProduct>
    where TMultiplicand : IMultipliable<TMultiplicand, TMultiplier, TProduct>
{
    TProduct __Mul__(TMultiplier other);

    static virtual TProduct operator *(TMultiplicand left, TMultiplier right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("*", "NoneType");
        }

        return left.__Mul__(right);
    }
}

public interface IMultipliable<TMultiplicand, TMultipilier> : IMultipliable<TMultiplicand, TMultipilier, TMultiplicand>
    where TMultiplicand : IMultipliable<TMultiplicand, TMultipilier, TMultiplicand>
{
}

public interface IMultipliable<T> : IMultipliable<T, T, T> where T : IMultipliable<T, T, T>
{
}
