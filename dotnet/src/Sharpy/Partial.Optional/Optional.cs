namespace Sharpy;

public partial struct Optional<T> : IBoolConvertible, IRepresentable, IEquatable, IEquatableWith<Optional<T>>
{
    private T _value;
    private bool _hasValue;

    public Optional() { _value = default; _hasValue = false; }

    public Optional(T value)
    {
        Set(value);
    }

    public T Value()
    {
        if (!_hasValue)
        {
            throw new InvalidOperationException($"Optional<{typeof(T).Name}> has no value.");
        }

        return _value;
    }

    public readonly T ValueOrDefault(T defaultValue)
    {
        return _hasValue ? _value : defaultValue!;
    }

    public Optional<U> Map<U>(Func<T, U> func)
    {
        if (!_hasValue)
        {
            return new Optional<U>();
        }

        return func(_value);
    }

    public void Set(T value)
    {
        _value = value ?? default;
        _hasValue = value is not null;
    }

    public T Take()
    {
        if (!_hasValue)
        {
            throw new InvalidOperationException($"Optional<{typeof(T).Name}> has no value.");
        }

        var value = _value;
        Clear();
        return value;
    }

    public readonly bool HasValue()
    {
        return _hasValue;
    }

    public void Clear()
    {
        _value = default;
        _hasValue = false;
    }

    public void Swap(Optional<T> other)
    {
        (other._value, _value) = (_value, other._value);
        (other._hasValue, _hasValue) = (_hasValue, other._hasValue);
    }

    public static bool operator true(Optional<T> optional)
    {
        return optional.__Bool__();
    }

    public static bool operator false(Optional<T> optional)
    {
        return !optional.__Bool__();
    }

    public static implicit operator Optional<T>(T value)
    {
        return new Optional<T>(value);
    }
}
