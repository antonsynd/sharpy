using System;
namespace Sharpy.Operator
{
    public static partial class Operator
    {
        public static bool Eq<T>(IComparable<T> left, T right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left?.CompareTo(right) == 0;
        }

        public static bool Eq(IComparable left, object right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left?.CompareTo(right) == 0;
        }

        public static bool Eq(object left, object right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left?.Equals(right) ?? right is null;
        }

        public static bool __Eq__<T>(IComparable<T> left, T right) => Eq(left, right);

        public static bool __Eq__(IComparable left, object right) => Eq(left, right);

        public static bool __Eq__(object left, object right) => Eq(left, right);
    }
}
