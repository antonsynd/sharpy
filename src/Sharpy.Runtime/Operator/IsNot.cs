namespace Sharpy.Operator;

public static partial class Exports
{
    public static bool IsNot(object left, object right)
    {
        return !ReferenceEquals(left, right);
    }
}
