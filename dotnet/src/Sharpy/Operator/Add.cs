namespace Sharpy.Operator;

public static partial class Exports
{
    public static T Add<T>(T left, T right) where T : IAddable<T>
    {
        return left.__Add__(right);
    }

    public static TSum Add<TLeft, TRight, TSum>(TLeft left, TRight right)
        where TLeft : IAddable<TLeft, TRight, TSum>
        where TRight : IAddable<TLeft, TRight, TSum>
        where TSum : IAddable<TLeft, TRight, TSum>
    {
        return left.__Add__(right);
    }

    public static T __Add__<T>(T left, T right) where T : IAddable<T> => Add<T>(left, right);

    public static TSum __Add__<TLeft, TRight, TSum>(TLeft left, TRight right)
        where TLeft : IAddable<TLeft, TRight, TSum>
        where TRight : IAddable<TLeft, TRight, TSum>
        where TSum : IAddable<TLeft, TRight, TSum>
        => Add<TLeft, TRight, TSum>(left, right);
}
