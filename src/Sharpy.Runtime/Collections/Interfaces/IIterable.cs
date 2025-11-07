namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for classes that provide the <see cref="__Iter__()"/> method.
/// </summary>
public interface IIterable<T> : IEnumerable<T>
{
    /// <summary>
    /// Return an <see cref="Iterator&lt;T&gt;"/> object. The object is required to
    /// support the iterator protocol described below.
    /// </summary>
    /// <remarks>
    /// If a container supports different types of iteration, additional
    /// methods can be provided to specifically request iterators for
    /// those iteration types. (An example of an object supporting multiple
    /// forms of iteration would be a tree structure which supports both
    /// breadth-first and depth-first traversal).
    /// </remarks>
    Iterator<T> __Iter__();
}
