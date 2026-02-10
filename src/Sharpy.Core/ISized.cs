namespace Sharpy
{
    /// <summary>
    /// Implemented by types that define __len__() in Sharpy.
    /// Provides a Count property for len() dispatch.
    /// Follows Python's collections.abc.Sized protocol.
    /// </summary>
    public interface ISized
    {
        int Count { get; }
    }
}
