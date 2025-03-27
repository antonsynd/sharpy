namespace Sharpy
{
    public static partial class Internals
    {
        public static bool __Eq__<T>(T left, T right) where T : Equatable<T>
        {
            return left?.__Eq__(right) ?? right is null;
        }

        public static bool __Eq__(object left, object right)
        {
            return left?.Equals(right) ?? right is null;
        }
    }
}
