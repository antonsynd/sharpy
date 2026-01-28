namespace Sharpy.Core
{
    public sealed partial class Set<T>
    {
        /// <summary>
        /// Deprecated: Use <c>set</c> in a boolean context (operator true/false) instead.
        /// </summary>
        public bool __Bool__()
        {
            return _set.Count > 0;
        }
    }
}
