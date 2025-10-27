namespace Sharpy;

public partial struct Optional<T> : IBoolConvertible, IRepresentable, IEquatable, IEquatableWith<Optional<T>>
{
    private T _value;
    private bool _hasValue;

    public Optional() { _value = default!; _hasValue = false; }

    public Optional(T value)
    {
        SetValue(value);
    }

    public readonly T Value()
    {
        if (!_hasValue)
        {
            throw new InvalidOperationException($"Optional<{typeof(T).Name}> has no value.");
        }

        return _value;
    }

    public readonly T ValueOrDefault() => ValueOr(default!);

    public readonly T ValueOr(T defaultValue)
    {
        return _hasValue ? _value : defaultValue;
    }

    public readonly Optional<U> MapValue<U>(Func<T, U> func)
    {
        if (!_hasValue)
        {
            return new Optional<U>();
        }

        return func(_value);
    }

    public void SetValue(T value)
    {
        _value = value ?? default!;
        _hasValue = value is not null;
    }

    public T Extract()
    {
        if (!_hasValue)
        {
            throw new InvalidOperationException($"Optional<{typeof(T).Name}> has no value.");
        }

        var value = _value;
        Clear();
        return value;
    }

    public T ExtractOr(T defaultValue)
    {
        if (!_hasValue)
        {
            return defaultValue;
        }

        var value = _value;
        Clear();
        return value;
    }

    public T ExtractOrDefault() => ExtractOr(default!);

    public readonly bool HasValue()
    {
        return _hasValue;
    }

    public void Clear()
    {
        _value = default!;
        _hasValue = false;
    }

    public void SwapWith(Optional<T> other)
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

    public static implicit operator T?(Optional<T> optional)
    {
        return optional.ValueOrDefault();
    }
}
