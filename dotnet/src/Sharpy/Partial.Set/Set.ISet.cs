namespace Sharpy
{
    public sealed partial class Set<T>
    {
        /// <inheritdoc/>
        public bool IsDisjoint(Set<T> other)
        {
            if (other is null)
            {
                // Not actually true here, but whatever
                throw new TypeError("'NoneType' object is not iterable");
            }

            return !_set.Overlaps(other._set);
        }

        /// <inheritdoc/>
        public Set<T> __And__(Set<T> other)
        {
            if (other is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            var result = new Set<T>();

            foreach (var item in _set)
            {
                if (other._set.Contains(item))
                {
                    result._set.Add(item);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public Set<T> __Or__(Set<T> other)
        {
            if (other is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            var result = new Set<T>(_set);

            foreach (var item in other._set)
            {
                result._set.Add(item);
            }

            return result;
        }

        /// <remarks>
        /// For insertion order, the other set is iterated through first.
        /// </remarks>
        public Set<T> __ROr__(Set<T> other)
        {
            if (other is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            var result = new Set<T>(other._set);

            foreach (var item in _set)
            {
                result._set.Add(item);
            }

            return result;
        }

        /// <inheritdoc/>
        public Set<T> __RSub__(Set<T> other)
        {
            if (other is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            var result = new Set<T>();

            foreach (var item in other._set)
            {
                if (!_set.Contains(item))
                {
                    result._set.Add(item);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public Set<T> __Sub__(Set<T> other)
        {
            if (other is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            var result = new Set<T>();

            foreach (var item in _set)
            {
                if (!other._set.Contains(item))
                {
                    result._set.Add(item);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public Set<T> __XOr__(Set<T> other)
        {
            if (other is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            var result = new Set<T>();

            foreach (var item in _set)
            {
                if (!other._set.Contains(item))
                {
                    result._set.Add(item);
                }
            }

            foreach (var item in other._set)
            {
                if (!_set.Contains(item))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public Collections.Interfaces.ISet<T> __And__(Collections.Interfaces.ISet<T> other)
        {
            if (other is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            var result = new Set<T>();

            foreach (var item in _set)
            {
                if (other.__Contains__(item))
                {
                    result._set.Add(item);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public Collections.Interfaces.ISet<T> __Or__(Collections.Interfaces.ISet<T> other)
        {
            var result = new Set<T>(this);

            foreach (var item in other)
            {
                result._set.Add(item);
            }

            return result;
        }

        /// <inheritdoc/>
        public Collections.Interfaces.ISet<T> __Sub__(Collections.Interfaces.ISet<T> other)
        {
            var result = new Set<T>();

            foreach (var elem in _set)
            {
                if (other.__Contains__(elem))
                {
                    continue;
                }

                result._set.Add(elem);
            }

            return result;
        }

        /// <inheritdoc/>
        public Collections.Interfaces.ISet<T> __RSub__(Collections.Interfaces.ISet<T> other)
        {
            var result = new Set<T>();

            foreach (var elem in other)
            {
                if (_set.Contains(elem))
                {
                    continue;
                }

                result._set.Add(elem);
            }

            return result;
        }

        /// <inheritdoc/>
        public Collections.Interfaces.ISet<T> __XOr__(Collections.Interfaces.ISet<T> other)
        {
            var result = new Set<T>();

            foreach (var elem in _set)
            {
                if (other.__Contains__(elem))
                {
                    continue;
                }

                result._set.Add(elem);
            }

            foreach (var elem in other)
            {
                if (_set.Contains(elem))
                {
                    continue;
                }

                result._set.Add(elem);
            }

            return result;
        }

        /// <inheritdoc/>
        public Collections.Interfaces.ISet<T> __ROr__(Collections.Interfaces.ISet<T> other)
        {
            var result = new Set<T>(other);
            result._set.UnionWith(_set);

            return result;
        }

        /// <inheritdoc/>
        public bool IsDisjoint(Collections.Interfaces.ISet<T> other)
        {
            if (other is null)
            {
                // Not actually true here, but whatever
                throw new TypeError("'NoneType' object is not iterable");
            }

            foreach (var item in other)
            {
                if (_set.Contains(item))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
