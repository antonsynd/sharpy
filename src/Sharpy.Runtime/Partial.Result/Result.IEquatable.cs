namespace Sharpy;

public partial struct Result<T, E>
{
    /// <remarks>
    /// Unlike other types, optionals are equivalent if they hold the same
    /// value.
    /// </remarks>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool __Eq__(object other)
    {
        if (other is Result<T, E> result)
        {
            return __Eq__(result);
        }

        return false;
    }

    public bool __Eq__(Result<T, E> other)
    {
        if (IsOk() != other.IsOk())
        {
            return false;
        }

        if (IsOk())
        {
            return Operator.Exports.Eq(_value, other._value);
        }
        else
        {
            return Operator.Exports.Eq(_error, other._error);
        }
    }

    public bool Equals(Result<T, E> other)
    {
        return __Eq__(other);
    }
}
