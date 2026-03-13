using System;

namespace Sharpy
{
    public static partial class Operator
    {
        /// <summary>Return true if left == right using IComparable&lt;T&gt;.</summary>
        public static bool Eq<T>(IComparable<T> left, T right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left?.CompareTo(right) == 0;
        }

        /// <summary>Return true if left == right using IComparable.</summary>
        public static bool Eq(IComparable left, object right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left?.CompareTo(right) == 0;
        }

        /// <summary>Return true if left == right using Equals.</summary>
        public static bool Eq(object left, object right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left?.Equals(right) ?? right is null;
        }
    }
}
