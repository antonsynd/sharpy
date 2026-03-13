namespace Sharpy
{
    /// <summary>
    /// Implemented by types that define __len__() in Sharpy.
    /// Provides a Count property for len() dispatch.
    /// Follows Python's collections.abc.Sized protocol.
    /// </summary>
    public interface ISized
    {
        /// <summary>Gets the number of elements in the collection.</summary>
        int Count { get; }
    }
}
