using System.Collections.Generic;
using System.Linq;
namespace Sharpy.Core
{
    public sealed partial class Set<T>
    {
        /// <remarks>
        /// For initializer literals and part of
        /// System.Collections.Generic.ICollection interface.
        /// </remarks>
        public void Add(T x)
        {
            _set.Add(x);
        }

        /// <inheritdoc/>
        public void Discard(T x)
        {
            _set.Remove(x);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _set.Clear();
        }

        /// <inheritdoc/>
        public T Pop()
        {
            if (_set.Count == 0)
            {
                throw new KeyError("pop from an empty set");
            }

            // Unsure about the efficiency of this
            var last = ((IEnumerable<T>)_set).Last();

            _set.Remove(last);

            return last;
        }

        /// <inheritdoc/>
        public void Remove(T x)
        {
            if (!_set.Remove(x))
            {
                throw new KeyError($"{x}");
            }
        }

        /// <inheritdoc/>
        public void __IAnd__(Set<T> other)
        {
            _set.IntersectWith(other._set);
        }

        /// <inheritdoc/>
        public void __IOr__(Set<T> other)
        {
            _set.UnionWith(other._set);
        }

        /// <inheritdoc/>
        public void __ISub__(Set<T> other)
        {
            _set.ExceptWith(other._set);
        }

        /// <inheritdoc/>
        public void __IXOr__(Set<T> other)
        {
            _set.SymmetricExceptWith(other._set);
        }
    }
}
