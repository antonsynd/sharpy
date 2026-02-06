namespace Sharpy
{
    public static partial class Operator
    {
        public static bool Is(object left, object right)
        {
            return ReferenceEquals(left, right);
        }
    }
}
