namespace Sharpy
{
    public sealed partial class SetIterator<T>
    {
        /// <inheritdoc/>
        public override bool MoveNext()
        {
            if (_setEnumerator.MoveNext())
            {
                _current = _setEnumerator.Current;
                return true;
            }

            _current = default;
            return false;
        }
    }
}
