namespace Sharpy;

/// <summary>
/// An interface for a type (the augend) that can be augmented (or added to)
/// with something (the addende) producing a sum (the result, which may be
/// the modified augend itself.
/// </summary>
/// <typeparam name="TAddend">The addend (the increase or thing to
/// add/insert).</typeparam>
/// <typeparam name="TSum">The sum.</typeparam>
public interface IAddableWith<TAddend, TSum>
{
    TSum __Add__(TAddend other);
}

/// <summary>
/// An interface for a type (the augend) that can be augmented (or added to)
/// with something (the addende) producing a sum (the result, which may be
/// the modified augend itself. This version has native operator support.
/// </summary>
/// <typeparam name="TAugend">The augend (the thing to add something to, or the
/// thing that is increased or augmented).</typeparam>
/// <typeparam name="TAddend">The addend (the increase or thing to
/// add/insert).</typeparam>
/// <typeparam name="TSum">The sum.</typeparam>
public interface IAddable<TAugend, TAddend, TSum> : IAddableWith<TAddend, TSum>
    where TAugend : IAddable<TAugend, TAddend, TSum>
{
    static virtual TSum operator +(TAugend left, TAddend right)
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
/// <typeparam name="TAugend">The type that gets augmented
/// (the "augend").</typeparam>
/// <typeparam name="TAddend">The type that indicates the amount of
/// augmentation or what is being included in the "augend".</typeparam>
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

/// <summary>
/// An interface for a type that can be added to itself yielding a result
/// of the same type.
/// </summary>
public interface IAddable<T> : IAddable<T, T, T> where T : IAddable<T, T, T>
{
}
