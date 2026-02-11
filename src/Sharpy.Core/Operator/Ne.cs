using System;

namespace Sharpy
{
    public static partial class Operator
    {
        public static bool Ne<T>(IComparable<T> left, T right)
        {
            if (ReferenceEquals(left, right))
            {
                return false;
            }

            return left?.CompareTo(right) != 0;
        }

        public static bool Ne(IComparable left, object right)
        {
            if (ReferenceEquals(left, right))
            {
                return false;
            }

            return left?.CompareTo(right) != 0;
        }

        public static bool Ne(object left, object right)
        {
            return !(left?.Equals(right) ?? right is null);
        }
    }
}
