namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public bool IsDisjoint(Set<T> other)
        {
            if (other is null)
            {
                // Not actually true here, but whatever
                throw new TypeError("'NoneType' object is not iterable");
            }

            return !_set.Overlaps(other._set);
        }

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
                    result.Add(item);
                }
            }

            return result;
        }

        public Set<T> __Or__(Set<T> other)
        {
            if (other is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            var result = new Set<T>(_set);

            foreach (var item in other._set)
            {
                result.Add(item);
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
                result.Add(item);
            }

            return result;
        }

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
                    result.Add(item);
                }
            }

            return result;
        }

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
                    result.Add(item);
                }
            }

            return result;
        }

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
                    result.Add(item);
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

        public Collections.Interfaces.Set<T> __And__(Collections.Interfaces.Set<T> other)
        {
            if (other is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            var result = new Set<T>();

            foreach (var item in _set)
            {
                if (other.Contains(item))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        public Collections.Interfaces.Set<T> __Or__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Collections.Interfaces.Set<T> __Sub__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Collections.Interfaces.Set<T> __RSub__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Collections.Interfaces.Set<T> __XOr__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Collections.Interfaces.Set<T> __ROr__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public bool IsDisjoint(Collections.Interfaces.Set<T> other)
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
