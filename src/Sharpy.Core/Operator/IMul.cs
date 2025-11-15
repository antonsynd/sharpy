using Sharpy.Core;
namespace Sharpy.Operator;

public static partial class Exports
{
    public static void IMul<T>(IInplaceMultipliable<T> left, T right)
    {
        left.__IMul__(right);
    }

    public static void IMul<TMultiplicand, TMultiplier>(ref TMultiplicand left, TMultiplier right)
        where TMultiplicand : IMultipliable<TMultiplicand, TMultiplier>
    {
        left = left.__Mul__(right);
    }

    public static void __IMul__<T>(IInplaceMultipliable<T> left, T right) => IMul<T>(left, right);

    public static void __IMul__<TMultiplicand, TMultiplier>(ref TMultiplicand left, TMultiplier right)
        where TMultiplicand : IMultipliable<TMultiplicand, TMultiplier>
        => IMul<TMultiplicand, TMultiplier>(ref left, right);
}
