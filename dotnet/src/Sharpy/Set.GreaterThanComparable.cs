namespace Sharpy
{
    public sealed partial class Set<T>
    {
        /// <inheritdoc/>
        public bool __Gt__(Set<T> other)
        {
            if (other is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }

            return _set.IsProperSupersetOf(other._set);
        }

        /// <inheritdoc/>
        public bool __Gt__(Collections.Interfaces.Set<T> other)
        {
            if (other is null)
            {
                throw new TypeError("'NoneType' object is not iterable");
            }


            var numElems = _set.Count;
            uint otherNumElems = 0;

            foreach (var x in other)
            {
                ++otherNumElems;

                if (!_set.Contains(x))
                {
                    return false;
                }
            }

            return otherNumElems < numElems;
        }
    }
}
