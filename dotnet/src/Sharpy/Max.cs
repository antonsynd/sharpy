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
            if (typeof(LessThanComparable<TKey>).IsAssignableFrom(typeof(TKey))) {
                bool iterableIsEmpty = true;
                T? biggest = default;

                foreach (var elem in iterable) {
                    // If this element is null, skip it, because null is the
                    // smallest element
                    if (elem == null) {
                        iterableIsEmpty = false;
                        continue;
                    }

                    if (biggest == null || iterableIsEmpty) {
                        biggest = elem;
                        iterableIsEmpty = false;

                        continue;
                    }

                    if (((LessThanComparable<TKey>)key(biggest)).__Lt__(key(elem))) {
                        biggest = elem;
                    }

                    // No-op, these are equivalent, no need to do anything
                }

                if (iterableIsEmpty) {
                    throw new ValueError("Max() iterable argument is empty");
                }

                return biggest;
            } else if (typeof(IComparable<TKey>).IsAssignableFrom(typeof(TKey))) {
                bool iterableIsEmpty = true;
                T? biggest = default;

                foreach (var elem in iterable) {
                    // If this element is null, skip it, because null is the
                    // smallest element
                    if (elem == null) {
                        iterableIsEmpty = false;
                        continue;
                    }

                    if (biggest == null || iterableIsEmpty) {
                        iterableIsEmpty = false;
                        biggest = elem;

                        continue;
                    }

                    if (((IComparable<TKey>)key(biggest)).CompareTo(key(elem)) > 0) {
                        continue;
                    }

                    biggest = elem;
                }

                if (iterableIsEmpty) {
                    throw new ValueError("Min() iterable argument is empty");
                }

                return biggest;
            }

            throw new TypeError($"'<' not supported for instances of ${typeof(TKey).Name}");
        }
    }
}
