namespace Sharpy;

public interface IRightMultipliable<TProduct, TMultiplicand>
{
    TProduct __RMul__(TMultiplicand other);
}
