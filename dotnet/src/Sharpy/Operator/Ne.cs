namespace Sharpy
{
    public static partial class Operator
    {
        public static bool __Ne__<T>(T left, T right) where T : IInequatable<T>
        {
            return left?.__Ne__(right) ?? right is null;
        }

        public static bool __Ne__(object left, object right)
        {
            return !(left?.Equals(right) ?? right is null);
        }
    }
}
