namespace Sharpy
{
    public static partial class Operator
    {
        public static bool IsNot(object left, object right)
        {
            return !ReferenceEquals(left, right);
        }
    }
}
