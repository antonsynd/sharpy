using System;

namespace Sharpy
{
    public static partial class Operator
    {
        /// <summary>Return true if left &gt; right using IComparable&lt;T&gt;.</summary>
        public static bool Gt<T>(IComparable<T> left, T right)
        {
            if (left is null || right is null)
            {
                throw TypeError.OpNotSupported(">", left, right);
            }

            return left.CompareTo(right) > 0;
        }

        /// <summary>Return true if left &gt; right using IComparable.</summary>
        public static bool Gt(IComparable left, object right)
        {
            if (left is null || right is null)
            {
                throw TypeError.OpNotSupported(">", left, right);
            }

            return left.CompareTo(right) > 0;
        }

        /// <summary>Return true if left &gt; right with automatic dispatch.</summary>
        public static bool Gt<T>(T left, T right)
        {
            // No same-object shortcut: python compares an object with itself too — `a <= a` is True
            // for a comparable a and a TypeError otherwise, never a skipped comparison (#2085).
            if (left is null || right is null)
            {
                throw TypeError.OpNotSupported(">", left, right);
            }

            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            {
                return Gt((IComparable<T>)left, right);
            }

            if (typeof(IComparable).IsAssignableFrom(typeof(T)))
            {
                return Gt((IComparable)left, right);
            }

            throw TypeError.OpNotSupported(">", left, right);
        }
    }
}
