using System;
using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Functools
    {
        /// <summary>Apply function of two arguments cumulatively to the items of iterable, with an initial value.</summary>
        public static T Reduce<T>(Func<T, T, T> func, IEnumerable<T> iterable, T initial)
        {
            T accumulator = initial;
            foreach (T item in iterable)
            {
                accumulator = func(accumulator, item);
            }

            return accumulator;
        }

        /// <summary>Transform a comparison function into a key function for use with sorted() and friends.</summary>
        public static IComparer<T> CmpToKey<T>(Func<T, T, int> func)
        {
            return new CmpComparer<T>(func);
        }

        /// <summary>Comparer that delegates to a user-supplied comparison function.</summary>
        private class CmpComparer<T> : IComparer<T>
        {
            private readonly Func<T, T, int> _func;

            public CmpComparer(Func<T, T, int> func)
            {
                _func = func;
            }

            /// <inheritdoc/>
            public int Compare(T? x, T? y)
            {
                return _func(x!, y!);
            }
        }
    }
}
