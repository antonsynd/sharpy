namespace Sharpy.Operator;
public static partial class Exports
{
    public static T Mul<T, U>(T left, U right) where T : IMultipliable<T, U>
    {
        return left.__Mul__(right);
    }

    public static T __Mul__<T, U>(T left, U right)
        where T : IMultipliable<T, U>
        => Mul(left, right);
}
