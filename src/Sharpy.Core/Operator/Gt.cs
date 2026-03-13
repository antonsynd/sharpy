using System;

namespace Sharpy
{
    public static partial class Operator
    {
        /// <summary>Return true if left &gt; right using IComparable&lt;T&gt;.</summary>
        public static bool Gt<T>(IComparable<T> left, T right)
        {
            if (ReferenceEquals(left, right))
            {
                return false;
            }

            if (left is null || right is null)
            {
                throw TypeError.OpNotSupported(">", "NoneType");
            }

            return left.CompareTo(right) > 0;
        }

        /// <summary>Return true if left &gt; right using IComparable.</summary>
        public static bool Gt(IComparable left, object right)
        {
            if (ReferenceEquals(left, right))
            {
                return false;
            }

            if (left is null || right is null)
            {
                throw TypeError.OpNotSupported(">", "NoneType");
            }

            return left.CompareTo(right) > 0;
        }

        /// <summary>Return true if left &gt; right with automatic dispatch.</summary>
        public static bool Gt<T>(T left, T right)
        {
            if (ReferenceEquals(left, right))
            {
                return false;
            }

            if (left is null || right is null)
            {
                throw TypeError.OpNotSupported(">", "NoneType");
            }

            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            {
                return Gt((IComparable<T>)left, right);
            }

            if (typeof(IComparable).IsAssignableFrom(typeof(T)))
            {
                return Gt((IComparable)left, right);
            }

            throw TypeError.OpNotSupported(">", typeof(T).Name);
        }
    }
}
