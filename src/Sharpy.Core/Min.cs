using System.Collections.Generic;
using System;
namespace Sharpy.Core
{

    public static partial class Builtins
    {
        public static T Min<T>(IEnumerable<T> iterable)
        {
            return Min(iterable, value => value);
        }

        public static T Min<T, TKey>(IEnumerable<T> iterable, Func<T, TKey> key)
        {
            if (iterable is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            if (key is null)
            {
                throw TypeError.ArgNone("min", "key");
            }

            bool iterableIsEmpty = true;
            T? smallest = default;

            foreach (var elem in iterable)
            {
                if (elem is null)
                {
                    throw TypeError.OpNotSupported("<", "NoneType");
                }

                if (smallest is null || iterableIsEmpty)
                {
                    smallest = elem;
                    iterableIsEmpty = false;

                    continue;
                }

                if (Operator.Operator.Lt(key(elem), key(smallest)))
                {
                    smallest = elem;
                }

                // No-op, these are equivalent, no need to do anything
            }

            if (smallest is null || iterableIsEmpty)
            {
                throw new ValueError("Min() iterable argument is empty");
            }

            return smallest;
        }
    }
}
