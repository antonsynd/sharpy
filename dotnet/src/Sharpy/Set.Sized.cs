namespace Sharpy
{
    public sealed partial class Set<T>
    {
        /// <inheritdoc/>
        public uint __Len__()
        {
            return (uint)_set.Count;
        }
    }
}
