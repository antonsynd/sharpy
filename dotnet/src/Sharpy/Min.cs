namespace Sharpy;

using Collections.Interfaces;
using Operator;

public static partial class Exports
{
    public static T Min<T>(IIterable<T> iterable) where T : ILessThanComparable<T>
    {
        return Min(iterable, value => value);
    }

    public static T Min<T, TKey>(IIterable<T> iterable, Func<T, TKey> key) where TKey : ILessThanComparable<TKey>
    {
        if (iterable is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        if (key is null)
        {
            throw new TypeError("Min() key argument cannot be None");
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

            if (Operator.Exports.Lt(key(elem), key(smallest)))
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
