namespace Sharpy
{
    public sealed partial class Set<T>
    {
        /// <inheritdoc/>
        public bool __Le__(Set<T> other)
        {
            if (other is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            return _set.IsSubsetOf(other._set);
        }

        /// <inheritdoc/>
        public bool __Le__(Collections.Interfaces.Set<T> other)
        {
            if (other is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            var numElems = _set.Count;
            uint otherNumElems = 0;

            if (numElems == otherNumElems)
            {
                return __Eq__(other);
            }
            else if (numElems > otherNumElems)
            {
                return false;
            }

            foreach (var x in other)
            {
                // TODO: It is possible that the other is implemented
                // incorrectly and has multiple copies of the same element,
                // in which case, we should check if we've seen it before.
                if (_set.Contains(x))
                {
                    --numElems;
                }
            }

            return numElems == 0;
        }
    }
}
