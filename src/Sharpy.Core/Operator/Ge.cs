using Sharpy.Core;
using System;
namespace Sharpy.Operator
{
    public static partial class Exports
    {
        public static bool Ge<T>(IComparable<T> left, T right)
        {
            if (ReferenceEquals(left, right))
            {
                return false;
            }

            if (left is null || right is null)
            {
                throw TypeError.OpNotSupported("<", "NoneType");
            }

            return left.CompareTo(right) >= 0;
        }

        public static bool Ge(IComparable left, object right)
        {
            if (ReferenceEquals(left, right))
            {
                return false;
            }

            if (left is null || right is null)
            {
                throw TypeError.OpNotSupported("<", "NoneType");
            }

            return left.CompareTo(right) >= 0;
        }

        public static bool Ge<T>(T left, T right)
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
                return Ge((IComparable<T>)left, right);
            }

            if (typeof(IComparable).IsAssignableFrom(typeof(T)))
            {
                return Ge((IComparable)left, right);
            }

            throw TypeError.OpNotSupported("<", typeof(T).Name);
        }

        public static bool __Ge__<T>(IComparable<T> left, T right) => Ge(left, right);

        public static bool __Ge__<T>(IComparable left, object right) => Ge(left, right);

        public static bool __Ge__<T>(T left, T right) => Ge(left, right);
    }
}
