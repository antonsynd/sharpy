using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class Builtins
    {
        public static T? Max<T>(Iterable<T> iterable)
        {
            return Max(iterable, value => value);
        }

        public static T? Max<T, TKey>(Iterable<T> iterable, Func<T, TKey> key)
        {
            if (iterable is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            if (key is null)
            {
                throw new TypeError("Max() key argument cannot be None");
            }

            bool iterableIsEmpty = true;
            T? biggest = default;

            foreach (var elem in iterable)
            {
                if (elem is null)
                {
                    throw new TypeError("'<' not supported for instances of 'NoneType'");
                }

                if (biggest is null || iterableIsEmpty)
                {
                    biggest = elem;
                    iterableIsEmpty = false;

                    continue;
                }

                if (LessThanAdapterFactory<TKey>.IsLessThan(key(biggest), key(elem)))
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
