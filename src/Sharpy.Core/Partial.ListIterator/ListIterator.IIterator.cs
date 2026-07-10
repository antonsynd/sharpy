namespace Sharpy
{
    public sealed partial class ListIterator<T>
    {
        /// <inheritdoc/>
        public override bool MoveNext()
        {
            // Read the backing store directly rather than going through the
            // Sharpy indexer, which normalizes (Index.Normalize) every element.
            var backing = _list._list;

            if (_index < backing.Count)
            {
                _current = backing[(int)_index];
                ++_index;
                return true;
            }

            _current = default;
            return false;
        }
    }
}
