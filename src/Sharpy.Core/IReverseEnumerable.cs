namespace Sharpy
{
    /// <summary>
    /// Implemented by types that define __reversed__() in Sharpy.
    /// Provides a GetReverseEnumerator() method for reversed() dispatch.
    /// </summary>
    public interface IReverseEnumerable<out T>
    {
        System.Collections.Generic.IEnumerator<T> GetReverseEnumerator();
    }
}
