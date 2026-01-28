using System.Collections.Generic;
using System;
namespace Sharpy.Core
{
    using Operator;

    public static partial class Exports
    {
        public static T Max<T>(IEnumerable<T> iterable)
        {
            return Max(iterable, value => value);
        }

        public static T Max<T, TKey>(IEnumerable<T> iterable, Func<T, TKey> key)
        {
            if (iterable is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            if (key is null)
            {
                throw TypeError.ArgNone("max", "key");
            }

            bool iterableIsEmpty = true;
            T? biggest = default;

            foreach (var elem in iterable)
            {
                if (elem is null)
                {
                    throw TypeError.OpNotSupported("<", "NoneType");
                }

                if (biggest is null || iterableIsEmpty)
                {
                    biggest = elem;
                    iterableIsEmpty = false;

                    continue;
                }

                if (Operator.Exports.Lt(key(biggest), key(elem)))
                {
                    biggest = elem;
                }

                // No-op, these are equivalent, no need to do anything
            }

            if (biggest is null || iterableIsEmpty)
            {
                throw new ValueError("Max() iterable argument is empty");
            }

            return biggest;
        }
    }
}
