using Sharpy.Core;
using System;
namespace Sharpy.Operator
{
    public static partial class Operator
    {
        public static bool Le<T>(IComparable<T> left, T right)
        {
            if (ReferenceEquals(left, right))
            {
                return false;
            }

            if (left is null || right is null)
            {
                throw TypeError.OpNotSupported("<", "NoneType");
            }

            return left.CompareTo(right) <= 0;
        }

        public static bool Le(IComparable left, object right)
        {
            if (ReferenceEquals(left, right))
            {
                return false;
            }

            if (left is null || right is null)
            {
                throw TypeError.OpNotSupported("<", "NoneType");
            }

            return left.CompareTo(right) <= 0;
        }

        public static bool Le<T>(T left, T right)
        {
            if (ReferenceEquals(left, right))
            {
                return false;
            }

            if (left is null || right is null)
            {
                throw TypeError.OpNotSupported("<", "NoneType");
            }

            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            {
                return Le((IComparable<T>)left, right);
            }

            if (typeof(IComparable).IsAssignableFrom(typeof(T)))
            {
                return Le((IComparable)left, right);
            }

            throw TypeError.OpNotSupported("<", typeof(T).Name);
        }

        public static bool __Le__<T>(IComparable<T> left, T right) => Le(left, right);

        public static bool __Le__<T>(IComparable left, object right) => Le(left, right);

        public static bool __Le__<T>(T left, T right) => Le(left, right);
    }
}
