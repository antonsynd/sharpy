namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for iterable classes that also provide the
    /// <see cref="__Reversed__()"/> method.
    /// </summary>
    public interface Reversible<T> : Iterable<T> where T : notnull
    {
        /// <summary>
        /// Called (if present) by the <see cref="Reversed()"/> built-in to
        /// implement reverse iteration. It should return a new
        /// <see cref="Iterator"/> that iterates over all the objects in the
        /// container in reverse order.
        /// </summary>
        Iterator<T> __Reversed__();
    }
}
