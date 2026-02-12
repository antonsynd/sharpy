using System.Collections.Generic;
namespace Sharpy
{
    using System.Collections;

    /// <summary>
    /// View of dictionary values.
    /// This view reflects changes to the underlying dictionary.
    /// </summary>
    public sealed class DictValuesView<K, V>
        : IReadOnlyCollection<V>
        where K : notnull
    {
        private readonly Dictionary<K, V>.ValueCollection _values;

        internal DictValuesView(Dictionary<K, V>.ValueCollection values)
        {
            _values = values;
        }

        /// <summary>
        /// Gets the number of values in the view.
        /// </summary>
        public int Count => _values.Count;

        /// <summary>
        /// Determines whether the view contains the specified value.
        /// </summary>
        /// <remarks>
        /// Values don't have a fast Contains check in .NET, so this iterates
        /// through all values using Sharpy's equality comparison.
        /// </remarks>
        public bool Contains(V item)
        {
            foreach (var value in _values)
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
            foreach (var value in _values)
            {
                yield return value;
            }
        }

        /// <summary>
        /// Deprecated: Use <see cref="Contains(V)"/> instead.
        /// </summary>
        public bool __Contains__(V item) => Contains(item);

        /// <summary>
        /// Deprecated: Use <see cref="GetEnumerator()"/> instead.
        /// </summary>
        public Iterator<V> __Iter__()
        {
            return new EnumeratorIterator<V>(GetEnumerator());
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
