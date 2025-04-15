namespace Sharpy.Operator;
public static partial class Exports
{
    public static T Mul<T>(T left, T right) where T : IMultipliable<T>
    {
        return left.__Mul__(right);
    }

    public static TMultiplicand Mul<TMultiplicand, TMultiplier>(TMultiplicand left, TMultiplier right)
        where TMultiplicand : IMultipliable<TMultiplicand, TMultiplier>
    {
        return left.__Mul__(right);
    }

    public static TProduct Mul<TMultiplicand, TMultiplier, TProduct>(TMultiplicand left, TMultiplier right)
        where TMultiplicand : IMultipliable<TMultiplicand, TMultiplier, TProduct>
    {
        return left.__Mul__(right);
    }

    public static T __Mul__<T>(T left, T right) where T : IMultipliable<T> => Mul(left, right);

    public static TMultiplicand __Mul__<TMultiplicand, TMultiplier>(TMultiplicand left, TMultiplier right)
        where TMultiplicand : IMultipliable<TMultiplicand, TMultiplier>
        => Mul<TMultiplicand, TMultiplier>(left, right);

    public static TProduct __Mul__<TMultiplicand, TMultiplier, TProduct>(TMultiplicand left, TMultiplier right)
        where TMultiplicand : IMultipliable<TMultiplicand, TMultiplier, TProduct>
        => Mul<TMultiplicand, TMultiplier, TProduct>(left, right);
}
