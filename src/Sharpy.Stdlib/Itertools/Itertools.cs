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
    public static partial class Itertools
    {
        public static System.Collections.Generic.IEnumerable<int> Count(int start = 0, int step = 1)
        {
            int n = start;
            while (true)
            {
                yield return n;
                n = n + step;
            }
        }

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
    }
}
