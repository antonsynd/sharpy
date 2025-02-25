namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for classes that provide the <see cref="__Contains__()"/>
    /// method.
    /// </summary>
    public interface Container<T>
    {
        /// <summary>
        /// Called to implement membership test operators. Should return
        /// <c>true</c> if item is in self, <c>false</c> otherwise. For
        /// mapping objects, this should consider the keys of the mapping
        /// rather than the values or the key-item pairs.
        /// </summary>
        bool __Contains__(T x);

        /// <remarks>
        /// In subclasses, this must call __Contains__(x) to correctly implement
        /// `x in y` behavior.
        /// </remarks>
        bool Contains(T x);
    }
}
