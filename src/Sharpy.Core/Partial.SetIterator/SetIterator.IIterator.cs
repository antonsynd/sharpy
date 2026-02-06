namespace Sharpy
{
    public sealed partial class SetIterator<T>
    {
        /// <summary>
        /// Deprecated: Use <see cref="Iterator{T}.Next()"/> instead.
        /// </summary>
        public override T __Next__()
        {
            if (_setEnumerator.MoveNext())
            {
                return _setEnumerator.Current;
            }

            throw new StopIteration();
        }
    }
}
