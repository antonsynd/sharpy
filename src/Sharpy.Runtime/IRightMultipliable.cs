namespace Sharpy;

public interface IRightMultipliableWith<TProduct, TMultiplier>
{
    TProduct __RMul__(TMultiplier other);
}

public interface IRightMultipliable<TMultiplicand, TMultiplier, TProduct>
    : IRightMultipliableWith<TProduct, TMultiplier>
      where TMultiplicand
        : IRightMultipliable<TMultiplicand, TMultiplier, TProduct>
{
    static virtual TProduct operator *(TMultiplier left, TMultiplicand right)
    {
        return right.__RMul__(left);
    }
}

public interface IRightMultipliable<TMultiplicand, TMultiplier>
    : IRightMultipliableWith<TMultiplicand, TMultiplier>,
      IRightMultipliable<TMultiplicand, TMultiplier, TMultiplicand>
      where TMultiplicand
        : IRightMultipliable<TMultiplicand, TMultiplier>
{
}
