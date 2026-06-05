// Generated from src/Sharpy.Stdlib/spy/itertools.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/itertools.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// Functions creating iterators for efficient looping.
    /// </summary>
    public static partial class Itertools
    {
        /// <summary>
        /// Make an iterator that returns evenly spaced values starting with number start.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<int> Count(int start = 0, int step = 1)
        {
            int n = start;
            while (true)
            {
                yield return n;
                n = n + step;
            }
        }

        /// <summary>
        /// Make an iterator that returns object over and over again, optionally limited by n times.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> Repeat<T>(T elem, int n = -1)
        {
            if (n < 0)
            {
                while (true)
                {
                    yield return elem;
                }
            }
            else
            {
                int i = 0;
                while (i < n)
                {
                    yield return elem;
                    i = i + 1;
                }
            }
        }

        /// <summary>
        /// Make an iterator returning elements from the iterable and saving a copy of each.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> Cycle<T>(Sharpy.List<T> iterable)
        {
            Sharpy.List<T> saved = new global::Sharpy.List<T>(iterable);
            if (global::Sharpy.Builtins.Len(saved) == 0)
            {
                yield break;
            }

            while (true)
            {
                foreach (var __loopVar_0 in saved)
                {
                    var element = __loopVar_0;
                    yield return element;
                }
            }
        }

        /// <summary>
        /// Make an iterator that filters elements from data returning only those that have a corresponding element in selectors that evaluates to True.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> Compress<T>(Sharpy.List<T> data, Sharpy.List<bool> selectors)
        {
            int i = 0;
            int limit = global::Sharpy.Builtins.Len(data);
            int selLimit = global::Sharpy.Builtins.Len(selectors);
            while (i < limit && i < selLimit)
            {
                if (selectors[i])
                {
                    yield return data[i];
                }

                i = i + 1;
            }
        }

        /// <summary>
        /// Make an iterator that drops elements from the iterable as long as the predicate is true; afterwards, returns every element.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> Dropwhile<T>(global::System.Func<T, bool> predicate, Sharpy.List<T> iterable)
        {
            bool dropping = true;
            foreach (var __loopVar_1 in iterable)
            {
                var element = __loopVar_1;
                if (dropping)
                {
                    if (!predicate(element))
                    {
                        dropping = false;
                        yield return element;
                    }
                }
                else
                {
                    yield return element;
                }
            }
        }

        /// <summary>
        /// Make an iterator that returns elements from the iterable as long as the predicate is true.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> Takewhile<T>(global::System.Func<T, bool> predicate, Sharpy.List<T> iterable)
        {
            foreach (var __loopVar_2 in iterable)
            {
                var element = __loopVar_2;
                if (predicate(element))
                {
                    yield return element;
                }
                else
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// Make an iterator that filters elements from iterable returning only those for which the predicate is false.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> Filterfalse<T>(global::System.Func<T, bool> predicate, Sharpy.List<T> iterable)
        {
            foreach (var __loopVar_3 in iterable)
            {
                var element = __loopVar_3;
                if (!predicate(element))
                {
                    yield return element;
                }
            }
        }

        /// <summary>
        /// Make an iterator that returns selected elements from the iterable.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> Islice<T>(Sharpy.List<T> iterable, int stop)
        {
            int i = 0;
            foreach (var __loopVar_4 in iterable)
            {
                var element = __loopVar_4;
                if (i >= stop)
                {
                    yield break;
                }

                yield return element;
                i = i + 1;
            }
        }

        /// <summary>
        /// Make an iterator that returns selected elements from the iterable with start, stop, and step.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> IsliceRange<T>(Sharpy.List<T> iterable, int start, int stop, int step = 1)
        {
            int i = 0;
            int nextYield = start;
            foreach (var __loopVar_5 in iterable)
            {
                var element = __loopVar_5;
                if (i >= stop)
                {
                    yield break;
                }

                if (i == nextYield)
                {
                    yield return element;
                    nextYield = nextYield + step;
                }

                i = i + 1;
            }
        }

        /// <summary>
        /// Return successive overlapping pairs taken from the input iterable.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<global::System.ValueTuple<T, T>> Pairwise<T>(Sharpy.List<T> iterable)
        {
            Sharpy.List<T> items = new global::Sharpy.List<T>(iterable);
            int i = 0;
            while (i < global::Sharpy.Builtins.Len(items) - 1)
            {
                yield return (items[i], items[i + 1]);
                i = i + 1;
            }
        }

        /// <summary>
        /// Make an iterator that returns accumulated sums.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<int> Accumulate(Sharpy.List<int> iterable)
        {
            Sharpy.List<int> items = new global::Sharpy.List<int>(iterable);
            if (global::Sharpy.Builtins.Len(items) == 0)
            {
                yield break;
            }

            int total = items[0];
            yield return total;
            int i = 1;
            while (i < global::Sharpy.Builtins.Len(items))
            {
                total = total + items[i];
                yield return total;
                i = i + 1;
            }
        }

        /// <summary>
        /// Make an iterator that returns accumulated results of a binary function.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> Accumulate<T>(Sharpy.List<T> iterable, global::System.Func<T, T, T> func)
        {
            Sharpy.List<T> items = new global::Sharpy.List<T>(iterable);
            if (global::Sharpy.Builtins.Len(items) == 0)
            {
                yield break;
            }

            T total = items[0];
            yield return total;
            int i = 1;
            while (i < global::Sharpy.Builtins.Len(items))
            {
                total = func(total, items[i]);
                yield return total;
                i = i + 1;
            }
        }

        /// <summary>
        /// Make an iterator that returns accumulated results of a binary function, starting with an initial value.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> Accumulate<T>(Sharpy.List<T> iterable, global::System.Func<T, T, T> func, T initial)
        {
            T total = initial;
            yield return total;
            foreach (var __loopVar_6 in iterable)
            {
                var item = __loopVar_6;
                total = func(total, item);
                yield return total;
            }
        }

        /// <summary>
        /// Make an iterator that returns elements from the first iterable until it is exhausted, then proceeds to the next iterable.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> Chain<T>(Sharpy.List<T> first, Sharpy.List<T> second)
        {
            foreach (var __loopVar_7 in first)
            {
                var x = __loopVar_7;
                yield return x;
            }

            foreach (var __loopVar_8 in second)
            {
                var y = __loopVar_8;
                yield return y;
            }
        }

        /// <summary>
        /// Make an iterator that returns elements from each iterable in turn until all are exhausted.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> Chain<T>(Sharpy.List<T> first, Sharpy.List<T> second, Sharpy.List<T> third)
        {
            foreach (var __loopVar_9 in first)
            {
                var x = __loopVar_9;
                yield return x;
            }

            foreach (var __loopVar_10 in second)
            {
                var y = __loopVar_10;
                yield return y;
            }

            foreach (var __loopVar_11 in third)
            {
                var z = __loopVar_11;
                yield return z;
            }
        }

        /// <summary>
        /// Make an iterator that chains all iterables from a single list of iterables.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> ChainFromIterable<T>(Sharpy.List<Sharpy.List<T>> iterables)
        {
            foreach (var __loopVar_12 in iterables)
            {
                var inner = __loopVar_12;
                foreach (var __loopVar_13 in inner)
                {
                    var item = __loopVar_13;
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Make an iterator that computes the function using arguments obtained from the iterable.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<R> Starmap<T1, T2, R>(global::System.Func<T1, T2, R> func, Sharpy.List<global::System.ValueTuple<T1, T2>> iterable)
        {
            foreach (var(a, b)in iterable)
            {
                yield return func(a, b);
            }
        }

        /// <summary>
        /// Make an iterator that aggregates elements from each iterable, filling missing values with fillvalue.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<global::System.ValueTuple<T, T>> ZipLongest<T>(Sharpy.List<T> first, Sharpy.List<T> second, T fillvalue)
        {
            int lenFirst = global::Sharpy.Builtins.Len(first);
            int lenSecond = global::Sharpy.Builtins.Len(second);
            int limit = lenFirst > lenSecond ? lenFirst : lenSecond;
            int i = 0;
            while (i < limit)
            {
                T x = i < lenFirst ? first[i] : fillvalue;
                T y = i < lenSecond ? second[i] : fillvalue;
                yield return (x, y);
                i = i + 1;
            }
        }

        /// <summary>
        /// Make an iterator that returns consecutive keys and groups from the iterable.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<global::System.ValueTuple<K, Sharpy.List<T>>> Groupby<T, K>(Sharpy.List<T> iterable, global::System.Func<T, K> key)
            where K : global::System.IEquatable<K>
        {
            Sharpy.List<T> items = new global::Sharpy.List<T>(iterable);
            if (global::Sharpy.Builtins.Len(items) == 0)
            {
                yield break;
            }

            K currentKey = key(items[0]);
            Sharpy.List<T> group = new Sharpy.List<T>()
            {
                items[0]
            };
            int i = 1;
            while (i < global::Sharpy.Builtins.Len(items))
            {
                K itemKey = key(items[i]);
                if (itemKey.Equals(currentKey))
                {
                    group.Append(items[i]);
                }
                else
                {
                    yield return (currentKey, group);
                    currentKey = itemKey;
                    group = new Sharpy.List<T>()
                    {
                        items[i]
                    };
                }

                i = i + 1;
            }

            yield return (currentKey, group);
        }

        /// <summary>
        /// Cartesian product of two input iterables, equivalent to nested for-loops.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<global::System.ValueTuple<T1, T2>> Product<T1, T2>(Sharpy.List<T1> first, Sharpy.List<T2> second)
        {
            foreach (var __loopVar_14 in first)
            {
                var x = __loopVar_14;
                foreach (var __loopVar_15 in second)
                {
                    var y = __loopVar_15;
                    yield return (x, y);
                }
            }
        }

        /// <summary>
        /// Cartesian product of three input iterables, equivalent to nested for-loops.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<global::System.ValueTuple<T1, T2, T3>> Product<T1, T2, T3>(Sharpy.List<T1> first, Sharpy.List<T2> second, Sharpy.List<T3> third)
        {
            foreach (var __loopVar_16 in first)
            {
                var x = __loopVar_16;
                foreach (var __loopVar_17 in second)
                {
                    var y = __loopVar_17;
                    foreach (var __loopVar_18 in third)
                    {
                        var z = __loopVar_18;
                        yield return (x, y, z);
                    }
                }
            }
        }

        /// <summary>
        /// Return successive r-length combinations of elements in the iterable.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<Sharpy.List<T>> Combinations<T>(Sharpy.List<T> iterable, int r)
        {
            if (r < 0)
            {
                throw new global::Sharpy.ValueError("r must be non-negative");
            }

            Sharpy.List<T> pool = new global::Sharpy.List<T>(iterable);
            int n = global::Sharpy.Builtins.Len(pool);
            if (r > n)
            {
                yield break;
            }

            Sharpy.List<int> indices = new Sharpy.List<int>()
            {
            };
            int i = 0;
            while (i < r)
            {
                indices.Append(i);
                i = i + 1;
            }

            Sharpy.List<Sharpy.List<T>> results = new Sharpy.List<Sharpy.List<T>>()
            {
            };
            Sharpy.List<T> first = new Sharpy.List<T>()
            {
            };
            int j = 0;
            while (j < r)
            {
                first.Append(pool[indices[j]]);
                j = j + 1;
            }

            results.Append(first);
            while (true)
            {
                i = r - 1;
                while (i >= 0 && indices[i] == n - r + i)
                {
                    i = i - 1;
                }

                if (i < 0)
                {
                    break;
                }

                indices[i] = indices[i] + 1;
                int k = i + 1;
                while (k < r)
                {
                    indices[k] = indices[k - 1] + 1;
                    k = k + 1;
                }

                Sharpy.List<T> combo = new Sharpy.List<T>()
                {
                };
                int m = 0;
                while (m < r)
                {
                    combo.Append(pool[indices[m]]);
                    m = m + 1;
                }

                results.Append(combo);
            }

            foreach (var __loopVar_19 in results)
            {
                var result = __loopVar_19;
                yield return result;
            }
        }

        /// <summary>
        /// Return successive r-length permutations of elements in the iterable. A negative r means full length.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<Sharpy.List<T>> Permutations<T>(Sharpy.List<T> iterable, int r = -1)
        {
            Sharpy.List<T> pool = new global::Sharpy.List<T>(iterable);
            int n = global::Sharpy.Builtins.Len(pool);
            int size = r < 0 ? n : r;
            if (size > n)
            {
                yield break;
            }

            Sharpy.List<int> indices = new Sharpy.List<int>()
            {
            };
            int i = 0;
            while (i < n)
            {
                indices.Append(i);
                i = i + 1;
            }

            Sharpy.List<int> cycles = new Sharpy.List<int>()
            {
            };
            int c = n;
            while (c > n - size)
            {
                cycles.Append(c);
                c = c - 1;
            }

            Sharpy.List<Sharpy.List<T>> results = new Sharpy.List<Sharpy.List<T>>()
            {
            };
            Sharpy.List<T> first = new Sharpy.List<T>()
            {
            };
            int j = 0;
            while (j < size)
            {
                first.Append(pool[indices[j]]);
                j = j + 1;
            }

            results.Append(first);
            bool done = n == 0;
            while (!done)
            {
                bool advanced = false;
                i = size - 1;
                while (i >= 0)
                {
                    cycles[i] = cycles[i] - 1;
                    if (cycles[i] == 0)
                    {
                        int rotated = indices[i];
                        int k = i;
                        while (k < n - 1)
                        {
                            indices[k] = indices[k + 1];
                            k = k + 1;
                        }

                        indices[n - 1] = rotated;
                        cycles[i] = n - i;
                    }
                    else
                    {
                        int swapIdx = n - cycles[i];
                        int temp = indices[i];
                        indices[i] = indices[swapIdx];
                        indices[swapIdx] = temp;
                        Sharpy.List<T> perm = new Sharpy.List<T>()
                        {
                        };
                        int m = 0;
                        while (m < size)
                        {
                            perm.Append(pool[indices[m]]);
                            m = m + 1;
                        }

                        results.Append(perm);
                        advanced = true;
                        break;
                    }

                    i = i - 1;
                }

                if (!advanced)
                {
                    done = true;
                }
            }

            foreach (var __loopVar_20 in results)
            {
                var result = __loopVar_20;
                yield return result;
            }
        }

        /// <summary>
        /// Return successive r-length combinations of elements in the iterable allowing individual elements to be repeated.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<Sharpy.List<T>> CombinationsWithReplacement<T>(Sharpy.List<T> iterable, int r)
        {
            if (r < 0)
            {
                throw new global::Sharpy.ValueError("r must be non-negative");
            }

            Sharpy.List<T> pool = new global::Sharpy.List<T>(iterable);
            int n = global::Sharpy.Builtins.Len(pool);
            if (n == 0 && r > 0)
            {
                yield break;
            }

            Sharpy.List<int> indices = new Sharpy.List<int>()
            {
            };
            int i = 0;
            while (i < r)
            {
                indices.Append(0);
                i = i + 1;
            }

            Sharpy.List<Sharpy.List<T>> results = new Sharpy.List<Sharpy.List<T>>()
            {
            };
            Sharpy.List<T> first = new Sharpy.List<T>()
            {
            };
            int j = 0;
            while (j < r)
            {
                first.Append(pool[indices[j]]);
                j = j + 1;
            }

            results.Append(first);
            while (true)
            {
                i = r - 1;
                while (i >= 0 && indices[i] == n - 1)
                {
                    i = i - 1;
                }

                if (i < 0)
                {
                    break;
                }

                int newVal = indices[i] + 1;
                int k = i;
                while (k < r)
                {
                    indices[k] = newVal;
                    k = k + 1;
                }

                Sharpy.List<T> combo = new Sharpy.List<T>()
                {
                };
                int m = 0;
                while (m < r)
                {
                    combo.Append(pool[indices[m]]);
                    m = m + 1;
                }

                results.Append(combo);
            }

            foreach (var __loopVar_21 in results)
            {
                var result = __loopVar_21;
                yield return result;
            }
        }
    }
}
