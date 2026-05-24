using System;
using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Itertools
    {
        public static IEnumerable<T> Repeat<T>(T elem, uint n)
        {
            return Repeat(elem, (int)n);
        }

        public static IEnumerable<T> Cycle<T>(IEnumerable<T> iterable)
        {
            return Cycle(new List<T>(iterable));
        }

        public static IEnumerable<T> Compress<T>(IEnumerable<T> data, IEnumerable<bool> selectors)
        {
            return Compress(new List<T>(data), new List<bool>(selectors));
        }

        public static IEnumerable<T> Dropwhile<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            return Dropwhile(predicate, new List<T>(iterable));
        }

        public static IEnumerable<T> Takewhile<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            return Takewhile(predicate, new List<T>(iterable));
        }

        public static IEnumerable<T> Filterfalse<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            return Filterfalse(predicate, new List<T>(iterable));
        }

        public static IEnumerable<(T, T)> Pairwise<T>(IEnumerable<T> iterable)
        {
            return Pairwise(new List<T>(iterable));
        }
    }
}
