namespace Sharpy;

public sealed partial class Optional<T>
{
    public override bool __Bool__()
    {
        return HasValue();
    }

    /// <remarks>
    /// Unlike other <see cref="Object"/> types, optionals are equivalent
    /// if they hold the same value.
    /// </remarks>
    /// <param name="other"></param>
    /// <returns></returns>
    public override bool __Eq__(Object? other)
    {
        if (other is Optional<T> optional)
        {
            return __Eq__(optional);
        }

        return false;
    }

    public bool __Eq__(Optional<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        return EqualityAdapterFactory<T>.AreEqual((T?)_value, (T?)other._value);
    }

    public override string __Repr__()
    {
        if (_value is null)
        {
            return "None";
        }

        return Repr(_value);
    }
}
