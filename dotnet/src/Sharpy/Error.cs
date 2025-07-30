namespace Sharpy;

public static partial class Exports
{
    public static Result<T, E> Error<T, E>(E error)
    {
        return new Result<T, E>(error);
    }
}
