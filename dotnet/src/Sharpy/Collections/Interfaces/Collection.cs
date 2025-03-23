namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for sized iterable container classes.
    /// </summary>
    public interface Collection<T> : Container<T>, Iterable<T>, Sized
    {
    }
}
