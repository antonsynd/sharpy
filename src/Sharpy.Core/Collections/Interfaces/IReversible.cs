using Sharpy.Core;

namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for iterable classes that also provide the
/// <see cref="__Reversed__()"/> method.
/// </summary>
public interface IReversible<T> : IIterable<T>
{
    /// <summary>
    /// Called (if present) by the <see cref="Reversed(IReversible&lt;T&gt;)"/> built-in to
    /// implement reverse iteration. It should return a new
    /// <see cref="Iterator&lt;T&gt;"/> that iterates over all the objects in the
    /// container in reverse order.
    /// </summary>
    Iterator<T> __Reversed__();
}
