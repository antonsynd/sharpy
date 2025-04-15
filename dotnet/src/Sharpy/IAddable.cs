namespace Sharpy;

public interface IAddable<TLeft, TRight, TSum> where TLeft : IAddable<TLeft, TRight, TSum>
{
    TSum __Add__(TRight other);

    static virtual TSum operator +(TLeft left, TRight right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("+", "NoneType");
        }

        return left.__Add__(right);
    }
}

/// <summary>
/// An interface defining an augmentation operator, where the left operand
/// is the element to be augmented (increased) and the right operand is the
/// amount of augumentation or something to be included in the other element.
/// </summary>
/// <typeparam name="TAugend">The type that gets augmented (the "augend").</typeparam>
/// <typeparam name="TAddend">The type that indicates the amount of augmentation or what is being included in the "augend".</typeparam>
public interface IAddable<TAugend, TAddend> where TAugend : IAddable<TAugend, TAddend>
{
    TAugend __Add__(TAddend other);

    static virtual TAugend operator +(TAugend left, TAddend right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("+", "NoneType");
        }

        return left.__Add__(right);
    }
}

public interface IAddable<T> : IAddable<T, T, T> where T : IAddable<T, T, T>
{
}
