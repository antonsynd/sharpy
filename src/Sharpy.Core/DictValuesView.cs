using System.Collections.Generic;
namespace Sharpy
{
    using System.Collections;

    /// <summary>
    /// View of dictionary values.
    /// This view reflects changes to the underlying dictionary.
    /// </summary>
    /// <remarks>
    /// Deliberately declares NO set-algebra operators, unlike <see cref="DictKeyView{K, V}"/> and
    /// <see cref="DictItemsView{K, V}"/>. Values are not necessarily unique or hashable, so CPython's
    /// <c>dict_values</c> is not set-like: <c>d.values() | e.values()</c> raises
    /// <c>TypeError: unsupported operand type(s)</c> (measured, python3.12). Sharpy refuses the same
    /// spellings at compile time (SPY0222) precisely because this type declares no operators — adding
    /// any here would silently accept what Python rejects (#1496).
    /// </remarks>
    public sealed class DictValuesView<K, V>
        : IReadOnlyCollection<V>,
          ISized
        where K : notnull
    {
        private readonly Dict<K, V> _owner;

        internal DictValuesView(Dict<K, V> owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// Gets the number of values in the view.
        /// </summary>
        public int Count => _owner.Count;

        /// <summary>
        /// Determines whether the view contains the specified value.
        /// </summary>
        public bool Contains(V item)
        {
            foreach (var value in this)
            {
                if (Operator.Eq(value, item))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the values.
        /// </summary>
        public IEnumerator<V> GetEnumerator()
        {
            var dict = _owner.InnerDict;
            int dictIndex = 0;
            foreach (var kvp in dict)
            {
                if (_owner.HasNullKey && dictIndex == _owner.NullOrdinal)
                {
                    yield return _owner.NullValue;
                }

                yield return kvp.Value;
                dictIndex++;
            }

            if (_owner.HasNullKey && _owner.NullOrdinal >= dict.Count)
            {
                yield return _owner.NullValue;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
