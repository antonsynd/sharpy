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
        public override TOut __Next__()
        {
            var value = _iterator.__Next__();
            return _function(value);
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
        public static MapIterator<TIn, TOut> Map<TIn, TOut>(Func<TIn, TOut> function, IEnumerable<TIn> iterable)
        {
            return new MapIterator<TIn, TOut>(function, iterable);
        }
    }
}
