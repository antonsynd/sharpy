namespace Sharpy.Operator;

public static partial class Exports
{
    public static bool Is_(object left, object right)
    {
        return ReferenceEquals(left, right);
    }
}
