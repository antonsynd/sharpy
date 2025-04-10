namespace Sharpy
{
    public static partial class Exports
    {
        public static bool Gt<T>(T left, T right) where T : IGreaterThanComparable<T>
        {
            if (ReferenceEquals(left, right))
            {
                return false;
            }

            if (left is null || right is null)
            {
                throw new TypeError("'<' not supported for 'NoneType' object");
            }

            return left.__Gt__(right);
        }

        public static bool __Gt__<T>(T left, T right) where T : IGreaterThanComparable<T> => Gt(left, right);
    }
}
