using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class Builtins
    {
        public static T Min<T>(Iterable<T> iterable) where T : notnull
        {
            return Min(iterable, value => value);
        }

        public static T Min<T, TKey>(Iterable<T> iterable, Func<T, TKey> key) where T : notnull where TKey : notnull
        {
            if (typeof(LessThanComparable<TKey>).IsAssignableFrom(typeof(TKey))) {
                bool iterableIsEmpty = true;
                T? smallest = default;

                foreach (var elem in iterable) {
                    if (smallest == null || iterableIsEmpty) {
                        iterableIsEmpty = false;
                        smallest = elem;

                        continue;
                    }

                    if (((LessThanComparable<TKey>)key(elem)).__Lt__(key(smallest))) {
                        smallest = elem;
                    }

                    // No-op, these are equivalent, no need to do anything
                }

                if (smallest == null || iterableIsEmpty) {
                    throw new ValueError("Min() iterable argument is empty");
                }

                return smallest;
            } else if (typeof(IComparable<TKey>).IsAssignableFrom(typeof(TKey))) {
                bool iterableIsEmpty = true;
                T? smallest = default;

                foreach (var elem in iterable) {
                    if (smallest == null || iterableIsEmpty) {
                        iterableIsEmpty = false;
                        smallest = elem;

                        continue;
                    }

                    if (((IComparable<TKey>)key(smallest)).CompareTo(key(elem)) <= 0) {
                        continue;
                    }

                    smallest = elem;
                }

                if (smallest == null || iterableIsEmpty) {
                    throw new ValueError("Min() iterable argument is empty");
                }

                return smallest;
            }

            throw new TypeError($"'<' not supported for instances of ${typeof(TKey).Name}");
        }
    }
}
