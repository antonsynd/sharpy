using System;
using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Functools
    {
        public static T Reduce<T>(Func<T, T, T> func, IEnumerable<T> iterable, T initial)
        {
            T accumulator = initial;
            foreach (T item in iterable)
            {
                accumulator = func(accumulator, item);
            }

            return accumulator;
        }

        public static IComparer<T> CmpToKey<T>(Func<T, T, int> func)
        {
            return new CmpComparer<T>(func);
        }

        private class CmpComparer<T> : IComparer<T>
        {
            private readonly Func<T, T, int> _func;

            public CmpComparer(Func<T, T, int> func)
            {
                _func = func;
            }

            public int Compare(T? x, T? y)
            {
                return _func(x!, y!);
            }
        }
    }
}
