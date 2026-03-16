using System.Collections.Generic;
namespace Sharpy
{
    /// <summary>
    /// Iterator that yields tuples containing elements from two iterables.
    /// </summary>
    /// <typeparam name="T1">The type of elements in the first iterable</typeparam>
    /// <typeparam name="T2">The type of elements in the second iterable</typeparam>
    public class ZipIterator<T1, T2> : Iterator<(T1, T2)>
    {
        private readonly Iterator<T1> _iterator1;
        private readonly Iterator<T2> _iterator2;
        private readonly bool _strict;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipIterator{T1, T2}"/> class.
        /// </summary>
        /// <param name="iterable1">The first iterable</param>
        /// <param name="iterable2">The second iterable</param>
        public ZipIterator(IEnumerable<T1> iterable1, IEnumerable<T2> iterable2)
            : this(iterable1, iterable2, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipIterator{T1, T2}"/> class.
        /// </summary>
        /// <param name="iterable1">The first iterable</param>
        /// <param name="iterable2">The second iterable</param>
        /// <param name="strict">If true, raises ValueError when iterables have different lengths</param>
        public ZipIterator(IEnumerable<T1> iterable1, IEnumerable<T2> iterable2, bool strict)
        {
            if (iterable1 is null)
            {
                throw TypeError.ArgNone("zip", "iterable1");
            }

            if (iterable2 is null)
            {
                throw TypeError.ArgNone("zip", "iterable2");
            }

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
                _current = (_iterator1.Current, _iterator2.Current);
                return true;
            }

            if (_strict && has1 != has2)
            {
                throw new ValueError(
                    "zip() has arguments with different lengths");
            }

            _current = default;
            return false;
        }
    }

    /// <summary>
    /// Iterator that yields tuples containing elements from three iterables.
    /// </summary>
    /// <typeparam name="T1">The type of elements in the first iterable</typeparam>
    /// <typeparam name="T2">The type of elements in the second iterable</typeparam>
    /// <typeparam name="T3">The type of elements in the third iterable</typeparam>
    public class ZipIterator<T1, T2, T3> : Iterator<(T1, T2, T3)>
    {
        private readonly Iterator<T1> _iterator1;
        private readonly Iterator<T2> _iterator2;
        private readonly Iterator<T3> _iterator3;
        private readonly bool _strict;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipIterator{T1, T2, T3}"/> class.
        /// </summary>
        /// <param name="iterable1">The first iterable</param>
        /// <param name="iterable2">The second iterable</param>
        /// <param name="iterable3">The third iterable</param>
        public ZipIterator(IEnumerable<T1> iterable1, IEnumerable<T2> iterable2, IEnumerable<T3> iterable3)
            : this(iterable1, iterable2, iterable3, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipIterator{T1, T2, T3}"/> class.
        /// </summary>
        /// <param name="iterable1">The first iterable</param>
        /// <param name="iterable2">The second iterable</param>
        /// <param name="iterable3">The third iterable</param>
        /// <param name="strict">If true, raises ValueError when iterables have different lengths</param>
        public ZipIterator(IEnumerable<T1> iterable1, IEnumerable<T2> iterable2, IEnumerable<T3> iterable3, bool strict)
        {
            if (iterable1 is null)
            {
                throw TypeError.ArgNone("zip", "iterable1");
            }

            if (iterable2 is null)
            {
                throw TypeError.ArgNone("zip", "iterable2");
            }

            if (iterable3 is null)
            {
                throw TypeError.ArgNone("zip", "iterable3");
            }

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
                _current = (_iterator1.Current, _iterator2.Current, _iterator3.Current);
                return true;
            }

            if (_strict && !(has1 == has2 && has2 == has3))
            {
                throw new ValueError(
                    "zip() has arguments with different lengths");
            }

            _current = default;
            return false;
        }
    }

    public static partial class Builtins
    {
        /// <summary>
        /// Make an iterator that aggregates elements from two iterables.
        /// Returns an iterator of tuples, where the i-th tuple contains the i-th element
        /// from each of the argument sequences. The iterator stops when the shortest
        /// input iterable is exhausted.
        /// </summary>
        /// <typeparam name="T1">The type of elements in the first iterable</typeparam>
        /// <typeparam name="T2">The type of elements in the second iterable</typeparam>
        /// <param name="iterable1">The first iterable</param>
        /// <param name="iterable2">The second iterable</param>
        /// <returns>A zip iterator</returns>
        /// <example>
        /// <code>
        /// list(zip([1, 2, 3], ["a", "b", "c"]))    # [(1, "a"), (2, "b"), (3, "c")]
        /// list(zip([1, 2], [10, 20, 30]))           # [(1, 10), (2, 20)]
        /// </code>
        /// </example>
        public static ZipIterator<T1, T2> Zip<T1, T2>(IEnumerable<T1> iterable1, IEnumerable<T2> iterable2)
        {
            return new ZipIterator<T1, T2>(iterable1, iterable2);
        }

        /// <summary>
        /// Make an iterator that aggregates elements from two iterables.
        /// When strict is true, raises ValueError if iterables have different lengths.
        /// </summary>
        /// <typeparam name="T1">The type of elements in the first iterable</typeparam>
        /// <typeparam name="T2">The type of elements in the second iterable</typeparam>
        /// <param name="iterable1">The first iterable</param>
        /// <param name="iterable2">The second iterable</param>
        /// <param name="strict">If true, raises ValueError when iterables have different lengths</param>
        /// <returns>A zip iterator</returns>
        public static ZipIterator<T1, T2> Zip<T1, T2>(IEnumerable<T1> iterable1, IEnumerable<T2> iterable2, bool strict)
        {
            return new ZipIterator<T1, T2>(iterable1, iterable2, strict);
        }

        /// <summary>
        /// Make an iterator that aggregates elements from three iterables.
        /// Returns an iterator of tuples, where the i-th tuple contains the i-th element
        /// from each of the argument sequences. The iterator stops when the shortest
        /// input iterable is exhausted.
        /// </summary>
        /// <typeparam name="T1">The type of elements in the first iterable</typeparam>
        /// <typeparam name="T2">The type of elements in the second iterable</typeparam>
        /// <typeparam name="T3">The type of elements in the third iterable</typeparam>
        /// <param name="iterable1">The first iterable</param>
        /// <param name="iterable2">The second iterable</param>
        /// <param name="iterable3">The third iterable</param>
        /// <returns>A zip iterator</returns>
        public static ZipIterator<T1, T2, T3> Zip<T1, T2, T3>(IEnumerable<T1> iterable1, IEnumerable<T2> iterable2, IEnumerable<T3> iterable3)
        {
            return new ZipIterator<T1, T2, T3>(iterable1, iterable2, iterable3);
        }

        /// <summary>
        /// Make an iterator that aggregates elements from three iterables.
        /// When strict is true, raises ValueError if iterables have different lengths.
        /// </summary>
        /// <typeparam name="T1">The type of elements in the first iterable</typeparam>
        /// <typeparam name="T2">The type of elements in the second iterable</typeparam>
        /// <typeparam name="T3">The type of elements in the third iterable</typeparam>
        /// <param name="iterable1">The first iterable</param>
        /// <param name="iterable2">The second iterable</param>
        /// <param name="iterable3">The third iterable</param>
        /// <param name="strict">If true, raises ValueError when iterables have different lengths</param>
        /// <returns>A zip iterator</returns>
        public static ZipIterator<T1, T2, T3> Zip<T1, T2, T3>(IEnumerable<T1> iterable1, IEnumerable<T2> iterable2, IEnumerable<T3> iterable3, bool strict)
        {
            return new ZipIterator<T1, T2, T3>(iterable1, iterable2, iterable3, strict);
        }
    }
}
