using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>Functions creating iterators for efficient looping.</summary>
    public static partial class Itertools
    {
        /// <summary>Make an iterator that returns object over and over again, limited by n times.</summary>
        public static IEnumerable<T> Repeat<T>(T elem, uint n)
        {
            return Repeat(elem, (int)n);
        }

        /// <summary>Make an iterator returning elements from the iterable and saving a copy of each.</summary>
        internal static IEnumerable<T> Cycle<T>(IEnumerable<T> iterable)
        {
            return Cycle(new List<T>(iterable));
        }

        /// <summary>Make an iterator that filters elements from data returning only those with a corresponding true selector.</summary>
        internal static IEnumerable<T> Compress<T>(IEnumerable<T> data, IEnumerable<bool> selectors)
        {
            return Compress(new List<T>(data), new List<bool>(selectors));
        }

        /// <summary>Make an iterator that drops elements as long as the predicate is true; afterwards, returns every element.</summary>
        internal static IEnumerable<T> Dropwhile<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            return Dropwhile(predicate, new List<T>(iterable));
        }

        /// <summary>Make an iterator that returns elements as long as the predicate is true.</summary>
        internal static IEnumerable<T> Takewhile<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            return Takewhile(predicate, new List<T>(iterable));
        }

        /// <summary>Make an iterator that filters elements returning only those for which the predicate is false.</summary>
        internal static IEnumerable<T> Filterfalse<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
        {
            return Filterfalse(predicate, new List<T>(iterable));
        }

        /// <summary>Return successive overlapping pairs taken from the input iterable.</summary>
        internal static IEnumerable<(T, T)> Pairwise<T>(IEnumerable<T> iterable)
        {
            return Pairwise(new List<T>(iterable));
        }

        /// <summary>Make an iterator that returns selected elements from the iterable.</summary>
        internal static IEnumerable<T> Islice<T>(IEnumerable<T> iterable, int stop)
        {
            return Islice(new Sharpy.List<T>(iterable), stop);
        }

        /// <summary>Make an iterator that returns selected elements from the iterable with start, stop, and step.</summary>
        internal static IEnumerable<T> Islice<T>(IEnumerable<T> iterable, int start, int stop, int step = 1)
        {
            return IsliceRange(new Sharpy.List<T>(iterable), start, stop, step);
        }
    }
}
