namespace Sharpy.Operator;

public static partial class Exports
{
    public static bool Lt<T>(T left, T right) where T : ILessThanComparable<T>
    {
        if (ReferenceEquals(left, right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            throw new TypeError("'<' not supported for 'NoneType' object");
        }

        return left.__Lt__(right);
    }

    public static bool __Lt__<T>(T left, T right) where T : ILessThanComparable<T> => Lt(left, right);
}
