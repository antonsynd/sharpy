using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class Builtins
    {
        public static T Min<T>(Iterable<T>? iterable)
        {
            return Min(iterable, value => value);
        }

        public static T Min<T, TKey>(Iterable<T>? iterable, Func<T, TKey>? key)
        {
            if (iterable is null) {
                throw new TypeError("'NoneType' object is not iterable");
            }

            if (key is null) {
                throw new TypeError("Min() key argument cannot be None");
            }

            bool iterableIsEmpty = true;
            T? smallest = default;

            foreach (var elem in iterable) {
                if (elem is null) {
                    throw new TypeError("'<' not supported for instances of 'NoneType'");
                }

                if (smallest is null || iterableIsEmpty) {
                    smallest = elem;
                    iterableIsEmpty = false;

                    continue;
                }

                if (LessThanAdapterFactory<TKey>.IsLessThan(key(elem), key(smallest)))
                {
                    smallest = elem;
                }

                // No-op, these are equivalent, no need to do anything
            }

            if (smallest is null || iterableIsEmpty) {
                throw new ValueError("Min() iterable argument is empty");
            }

            return smallest;
        }
    }
}
