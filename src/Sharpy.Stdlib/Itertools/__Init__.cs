using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    /// <summary>Functions creating iterators for efficient looping.</summary>
    [SharpyModule("itertools")]
    public static partial class Itertools
    {
        // ---- Repeat uint adapter ----

        /// <summary>Make an iterator that returns object over and over again, limited by n times.</summary>
        public static IEnumerable<T> Repeat<T>(T elem, uint n)
        {
            return Repeat(elem, (int)n);
        }

        // ---- IEnumerable<T> → Sharpy.List<T> adapter overloads ----

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

        // ---- Accumulate IEnumerable<T> adapter overloads ----

        /// <summary>Make an iterator that returns accumulated sums (or accumulated results of a binary function).</summary>
        public static IEnumerable<T> Accumulate<T>(IEnumerable<T> iterable, Func<T, T, T> func)
        {
            return Accumulate(new Sharpy.List<T>(iterable), func);
        }

        /// <summary>Make an iterator that returns accumulated results with an initial value.</summary>
        public static IEnumerable<T> Accumulate<T>(IEnumerable<T> iterable, Func<T, T, T> func, T initial)
        {
            return Accumulate(new Sharpy.List<T>(iterable), func, initial);
        }

        // ---- Multi-arg chain ----

        /// <summary>
        /// Make an iterator that returns elements from the first iterable until it is exhausted,
        /// then proceeds to the next iterable.
        /// </summary>
        /// <param name="iterables">One or more iterables to chain together.</param>
        /// <typeparam name="T">The element type.</typeparam>
        /// <returns>An iterator over the concatenated elements.</returns>
        public static IEnumerable<T> Chain<T>(params IEnumerable<T>[] iterables)
        {
            foreach (var iterable in iterables)
            {
                foreach (var item in iterable)
                {
                    yield return item;
                }
            }
        }

        // ---- Combinatorics IEnumerable<T> adapter overloads ----

        /// <summary>Return r-length combinations of elements in the iterable.</summary>
        public static IEnumerable<Sharpy.List<T>> Combinations<T>(IEnumerable<T> iterable, int r)
        {
            return Combinations(new Sharpy.List<T>(iterable), r);
        }

        /// <summary>Return successive r-length permutations of elements in the iterable.</summary>
        public static IEnumerable<Sharpy.List<T>> Permutations<T>(IEnumerable<T> iterable, int r = -1)
        {
            return Permutations(new Sharpy.List<T>(iterable), r);
        }

        /// <summary>Return r-length combinations of elements allowing individual elements to be repeated.</summary>
        public static IEnumerable<Sharpy.List<T>> CombinationsWithReplacement<T>(IEnumerable<T> iterable, int r)
        {
            return CombinationsWithReplacement(new Sharpy.List<T>(iterable), r);
        }

        // ---- Starmap / Groupby IEnumerable<T> adapter overloads ----

        /// <summary>Make an iterator that computes the function using arguments obtained from the iterable.</summary>
        public static IEnumerable<R> Starmap<T1, T2, R>(
            Func<T1, T2, R> func, IEnumerable<(T1, T2)> iterable)
        {
            return Starmap(func, new Sharpy.List<(T1, T2)>(iterable));
        }
    }
}
