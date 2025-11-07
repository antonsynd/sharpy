namespace Sharpy;

public partial struct Optional<T>
{
    /// <remarks>
    /// Unlike other types, optionals are equivalent if they hold the same
    /// value.
    /// </remarks>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool __Eq__(object other)
    {
        if (other is Optional<T> optional)
        {
            return __Eq__(optional);
        }

        return false;
    }

    public bool __Eq__(Optional<T> other)
    {
        return Operator.Exports.Eq(_value, other._value);
    }

    public bool Equals(Optional<T> other)
    {
        return __Eq__(other);
    }
}
