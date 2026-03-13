namespace Sharpy
{
    /// <summary>
    /// Implemented by types that define __reversed__() in Sharpy.
    /// Provides a GetReverseEnumerator() method for reversed() dispatch.
    /// </summary>
    public interface IReverseEnumerable<out T>
    {
        /// <summary>Returns an enumerator that iterates through the collection in reverse order.</summary>
        System.Collections.Generic.IEnumerator<T> GetReverseEnumerator();
    }
}
