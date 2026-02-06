using Sharpy.Core;
using System;
namespace Sharpy.Operator
{
    public static partial class Operator
    {
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

        public static bool __Gt__<T>(IComparable<T> left, T right) => Gt(left, right);

        public static bool __Gt__(IComparable left, object right) => Gt(left, right);

        public static bool __Gt__<T>(T left, T right) => Gt(left, right);
    }
}
