namespace Sharpy;

public readonly partial struct Result<T, E> : IBoolConvertible, IRepresentable, IEquatable, IEquatableWith<Result<T, E>>
{
    private readonly T _value;
    private readonly E _error;
    private readonly bool _isOk;

    public Result(T value)
    {
        _value = value;
        _error = default!;
        _isOk = true;
    }

    public Result(E error)
    {
        _value = default!;
        _error = error;
        _isOk = false;
    }

    public readonly bool IsOk()
    {
        return _isOk;
    }

    public readonly bool IsError()
    {
        return !_isOk;
    }

    public readonly Optional<T> Value()
    {
        return _isOk ? Some<T>(_value) : None<T>();
    }

    public readonly Optional<E> Error()
    {
        return _isOk ? None<E>() : Some<E>(_error);
    }

    public static bool operator true(Result<T, E> result)
    {
        return result.__Bool__();
    }

    public static bool operator false(Result<T, E> result)
    {
        return !result.__Bool__();
    }

    public static implicit operator Result<T, E>(T value)
    {
        return new Result<T, E>(value);
    }

    public static implicit operator Result<T, E>(E error)
    {
        return new Result<T, E>(error);
    }
}
