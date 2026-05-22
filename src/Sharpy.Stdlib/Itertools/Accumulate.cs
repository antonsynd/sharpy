using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>Itertools module — tools for creating iterators.</summary>
    public static partial class Itertools
    {
        /// <summary>Make an iterator that returns accumulated sums (or accumulated results of a binary function).</summary>
        public static AccumulateIterator<T> Accumulate<T>(IEnumerable<T> iterable, Func<T, T, T>? func = null)
        {
            return new AccumulateIterator<T>(iterable, func, default, false);
        }

        /// <summary>Make an iterator that returns accumulated results with an initial value.</summary>
        public static AccumulateIterator<T> Accumulate<T>(IEnumerable<T> iterable, Func<T, T, T>? func, T initial)
        {
            return new AccumulateIterator<T>(iterable, func, initial, true);
        }
    }

    /// <summary>Iterator that yields accumulated sums or results of a binary function.</summary>
    public class AccumulateIterator<T> : Iterator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly Func<T, T, T> _func;
        private T? _accumulator;
        private bool _hasInitial;
        private bool _started;
        private bool _exhausted;

        internal AccumulateIterator(IEnumerable<T> iterable, Func<T, T, T>? func, T? initial, bool hasInitial)
        {
            _enumerator = iterable.GetEnumerator();
            _func = func ?? DefaultAdd;
            _hasInitial = hasInitial;
            _accumulator = initial;
            _started = false;
            _exhausted = false;
        }

        private static T DefaultAdd(T a, T b)
        {
            if (a is int ai && b is int bi)
            {
                return (T)(object)(ai + bi);
            }
            if (a is long al && b is long bl)
            {
                return (T)(object)(al + bl);
            }
            if (a is double ad && b is double bd)
            {
                return (T)(object)(ad + bd);
            }
            if (a is float af && b is float bf)
            {
                return (T)(object)(af + bf);
            }

            throw new TypeError("unsupported operand type(s) for accumulate default function");
        }

        /// <inheritdoc/>
        public override bool MoveNext()
        {
            if (_exhausted)
            {
                _current = default;
                return false;
            }

            if (!_started)
            {
                _started = true;
                if (_hasInitial)
                {
                    _current = _accumulator!;
                    return true;
                }

                if (!_enumerator.MoveNext())
                {
                    _exhausted = true;
                    _current = default;
                    return false;
                }

                _accumulator = _enumerator.Current;
                _current = _accumulator;
                return true;
            }

            if (!_enumerator.MoveNext())
            {
                _exhausted = true;
                _current = default;
                return false;
            }

            _accumulator = _func(_accumulator!, _enumerator.Current);
            _current = _accumulator;
            return true;
        }
    }
}
