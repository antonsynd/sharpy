namespace Sharpy
{
    internal sealed partial class SetIterator<T>(HashSet<T> set) : Iterator<T> where T : notnull
    {
        private readonly HashSet<T>.Enumerator _enumerator = set.GetEnumerator();

        public override T __Next__()
        {
            try
            {
                _enumerator.MoveNext();
            }
            catch (InvalidOperationException)
            {
                throw new StopIteration();
            }

            return _enumerator.Current;
        }
    }
}
