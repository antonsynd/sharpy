namespace Sharpy
{
    public static partial class Internals
    {
        public static bool __Lt__<T>(T left, T right) where T : LessThanComparable<T>
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

        // public static bool __Lt__<U>(T left, T right) where T : Comparable<T>
        // {
        //     left.
        // }
    }
}
