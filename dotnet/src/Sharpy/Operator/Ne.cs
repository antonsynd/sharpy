namespace Sharpy.Operator;

public static partial class Exports
{
    public static bool Ne<T>(T left, T right) where T : IInequatable<T>
    {
        return left?.__Ne__(right) ?? right is null;
    }

    public static bool Ne(object left, object right)
    {
        return !(left?.Equals(right) ?? right is null);
    }

    public static bool __Ne__<T>(T left, T right) where T : IInequatable<T> => Ne(left, right);
    public static bool __Ne__(object left, object right) => Ne(left, right);
}
