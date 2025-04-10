namespace Sharpy
{
    public static partial class Exports
    {
        public static bool Le<T>(T left, T right) where T : ILessThanOrEquatable<T>
        {
            if (ReferenceEquals(left, right))
            {
                return false;
            }

            if (left is null || right is null)
            {
                throw new TypeError("'<' not supported for 'NoneType' object");
            }

            return left.__Le__(right);
        }

        public static bool __Le__<T>(T left, T right) where T : ILessThanOrEquatable<T> => Le(left, right);
    }
}
