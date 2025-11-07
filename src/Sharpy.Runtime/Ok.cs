namespace Sharpy;

public static partial class Exports
{
    public static Result<T, E> Ok<T, E>(T value)
    {
        return new Result<T, E>(value);
    }
}
