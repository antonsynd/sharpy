namespace Sharpy;

public interface IAddable<TLeft, TRight, TSum>
    where TLeft : IAddable<TLeft, TRight, TSum>
    where TRight : IAddable<TLeft, TRight, TSum>
    where TSum : IAddable<TLeft, TRight, TSum>
{
    TSum __Add__(TRight other);

    static virtual TSum operator +(TLeft left, TRight right)
    {
        if (left is null || right is null)
        {
            throw new TypeError("'+' is not supported for objects of type 'NoneType'");
        }

        return left.__Add__(right);
    }
}

public interface IAddable<T> : IAddable<T, T, T> where T : IAddable<T, T, T>
{
}
