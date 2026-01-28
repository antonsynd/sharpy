namespace Sharpy.Core
{
    public sealed partial class Set<T>
    {
        /// <inheritdoc/>
        public bool __Ne__(Set<T> other)
        {
            return !__Eq__(other);
        }
    }
}
