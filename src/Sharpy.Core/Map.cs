using System.Collections.Generic;
using System;
namespace Sharpy
{
    /// <summary>
    /// Iterator that yields the result of applying a function to elements from an iterable.
    /// </summary>
    /// <typeparam name="TIn">The type of elements in the input iterable</typeparam>
    /// <typeparam name="TOut">The type of elements in the output</typeparam>
    public class MapIterator<TIn, TOut> : Iterator<TOut>
    {
        private readonly Iterator<TIn> _iterator;
        private readonly Func<TIn, TOut> _function;

        /// <summary>
        /// Initializes a new instance of the <see cref="MapIterator{TIn, TOut}"/> class.
        /// </summary>
        /// <param name="function">The function to apply to each element</param>
        /// <param name="iterable">The iterable to map over</param>
        public MapIterator(Func<TIn, TOut> function, IEnumerable<TIn> iterable)
        {
            if (function is null)
            {
                throw TypeError.ArgNone("map", "function");
            }

            if (iterable is null)
            {
                throw TypeError.ArgNone("map", "iterable");
            }

            _function = function;
            _iterator = Builtins.Iter(iterable);
        }

        /// <inheritdoc/>
        public override bool MoveNext()
        {
            if (!_iterator.MoveNext())
            {
                _current = default;
                return false;
            }

            _current = _function(_iterator.Current);
            return true;
        }
    }

    /// <summary>
    /// Iterator that applies a two-argument function to elements drawn in parallel from two
    /// iterables. Mirrors <see cref="ZipIterator{T1, T2}"/>'s lazy, optionally-strict iteration.
    /// </summary>
    /// <typeparam name="T1">The type of elements in the first iterable</typeparam>
    /// <typeparam name="T2">The type of elements in the second iterable</typeparam>
    /// <typeparam name="TOut">The type of elements in the output</typeparam>
    public class MapIterator<T1, T2, TOut> : Iterator<TOut>
    {
        private readonly Iterator<T1> _iterator1;
        private readonly Iterator<T2> _iterator2;
        private readonly Func<T1, T2, TOut> _function;
        private readonly bool _strict;

        /// <summary>
        /// Initializes a new instance of the <see cref="MapIterator{T1, T2, TOut}"/> class.
        /// </summary>
        public MapIterator(Func<T1, T2, TOut> function, IEnumerable<T1> iterable1, IEnumerable<T2> iterable2)
            : this(function, iterable1, iterable2, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MapIterator{T1, T2, TOut}"/> class.
        /// </summary>
        public MapIterator(Func<T1, T2, TOut> function, IEnumerable<T1> iterable1, IEnumerable<T2> iterable2, bool strict)
        {
            if (function is null)
            {
                throw TypeError.ArgNone("map", "function");
            }

            if (iterable1 is null)
            {
                throw TypeError.ArgNone("map", "iterable1");
            }

            if (iterable2 is null)
            {
                throw TypeError.ArgNone("map", "iterable2");
            }

            _function = function;
            _iterator1 = Builtins.Iter(iterable1);
            _iterator2 = Builtins.Iter(iterable2);
            _strict = strict;
        }

        /// <inheritdoc/>
        public override bool MoveNext()
        {
            bool has1 = _iterator1.MoveNext();
            bool has2 = _iterator2.MoveNext();

            if (has1 && has2)
            {
                _current = _function(_iterator1.Current, _iterator2.Current);
                return true;
            }

            if (_strict && has1 != has2)
            {
                throw new ValueError(
                    "map() has arguments with different lengths");
            }

            _current = default;
            return false;
        }
    }

    /// <summary>
    /// Iterator that applies a three-argument function to elements drawn in parallel from three
    /// iterables. Mirrors <see cref="ZipIterator{T1, T2, T3}"/>'s lazy, optionally-strict iteration.
    /// </summary>
    /// <typeparam name="T1">The type of elements in the first iterable</typeparam>
    /// <typeparam name="T2">The type of elements in the second iterable</typeparam>
    /// <typeparam name="T3">The type of elements in the third iterable</typeparam>
    /// <typeparam name="TOut">The type of elements in the output</typeparam>
    public class MapIterator<T1, T2, T3, TOut> : Iterator<TOut>
    {
        private readonly Iterator<T1> _iterator1;
        private readonly Iterator<T2> _iterator2;
        private readonly Iterator<T3> _iterator3;
        private readonly Func<T1, T2, T3, TOut> _function;
        private readonly bool _strict;

        /// <summary>
        /// Initializes a new instance of the <see cref="MapIterator{T1, T2, T3, TOut}"/> class.
        /// </summary>
        public MapIterator(Func<T1, T2, T3, TOut> function, IEnumerable<T1> iterable1, IEnumerable<T2> iterable2, IEnumerable<T3> iterable3)
            : this(function, iterable1, iterable2, iterable3, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MapIterator{T1, T2, T3, TOut}"/> class.
        /// </summary>
        public MapIterator(Func<T1, T2, T3, TOut> function, IEnumerable<T1> iterable1, IEnumerable<T2> iterable2, IEnumerable<T3> iterable3, bool strict)
        {
            if (function is null)
            {
                throw TypeError.ArgNone("map", "function");
            }

            if (iterable1 is null)
            {
                throw TypeError.ArgNone("map", "iterable1");
            }

            if (iterable2 is null)
            {
                throw TypeError.ArgNone("map", "iterable2");
            }

            if (iterable3 is null)
            {
                throw TypeError.ArgNone("map", "iterable3");
            }

            _function = function;
            _iterator1 = Builtins.Iter(iterable1);
            _iterator2 = Builtins.Iter(iterable2);
            _iterator3 = Builtins.Iter(iterable3);
            _strict = strict;
        }

        /// <inheritdoc/>
        public override bool MoveNext()
        {
            bool has1 = _iterator1.MoveNext();
            bool has2 = _iterator2.MoveNext();
            bool has3 = _iterator3.MoveNext();

            if (has1 && has2 && has3)
            {
                _current = _function(_iterator1.Current, _iterator2.Current, _iterator3.Current);
                return true;
            }

            if (_strict && !(has1 == has2 && has2 == has3))
            {
                throw new ValueError(
                    "map() has arguments with different lengths");
            }

            _current = default;
            return false;
        }
    }

    public static partial class Builtins
    {
        /// <summary>
        /// Return an iterator that applies function to every item of iterable, yielding the results.
        /// </summary>
        /// <typeparam name="TIn">The type of elements in the input iterable</typeparam>
        /// <typeparam name="TOut">The type of elements in the output</typeparam>
        /// <param name="function">The function to apply to each element</param>
        /// <param name="iterable">The iterable to map over</param>
        /// <returns>A map iterator</returns>
        /// <example>
        /// <code>
        /// list(map(lambda x: x * 2, [1, 2, 3]))    # [2, 4, 6]
        /// list(map(str, [1, 2, 3]))                 # ["1", "2", "3"]
        /// </code>
        /// </example>
        public static MapIterator<TIn, TOut> Map<TIn, TOut>(Func<TIn, TOut> function, IEnumerable<TIn> iterable)
        {
            return new MapIterator<TIn, TOut>(function, iterable);
        }

        /// <summary>
        /// Return an iterator that applies a two-argument function to corresponding items of two
        /// iterables. With <paramref name="strict"/> true, raises ValueError if the iterables have
        /// different lengths (Python 3.14 behaviour); otherwise stops at the shortest.
        /// </summary>
        /// <example>
        /// <code>
        /// list(map(lambda a, b: a + b, [1, 2], [10, 20]))             # [11, 22]
        /// list(map(lambda a, b: a + b, [1, 2], [10], strict=True))    # ValueError
        /// </code>
        /// </example>
        public static MapIterator<T1, T2, TOut> Map<T1, T2, TOut>(Func<T1, T2, TOut> function, IEnumerable<T1> iterable1, IEnumerable<T2> iterable2, bool strict = false)
        {
            return new MapIterator<T1, T2, TOut>(function, iterable1, iterable2, strict);
        }

        /// <summary>
        /// Return an iterator that applies a three-argument function to corresponding items of three
        /// iterables. With <paramref name="strict"/> true, raises ValueError if the iterables have
        /// different lengths; otherwise stops at the shortest.
        /// </summary>
        public static MapIterator<T1, T2, T3, TOut> Map<T1, T2, T3, TOut>(Func<T1, T2, T3, TOut> function, IEnumerable<T1> iterable1, IEnumerable<T2> iterable2, IEnumerable<T3> iterable3, bool strict = false)
        {
            return new MapIterator<T1, T2, T3, TOut>(function, iterable1, iterable2, iterable3, strict);
        }
    }
}
