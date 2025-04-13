namespace Sharpy.Operator;

public static partial class Exports
{
    public static bool Ge<T>(T left, T right) where T : IGreaterThanOrEquatable<T>
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw new TypeError("'<' not supported for 'NoneType' object");
        }

        return left.__Ge__(right);
    }

    public static bool __Ge__<T>(T left, T right) where T : IGreaterThanOrEquatable<T> => Ge(left, right);
}
