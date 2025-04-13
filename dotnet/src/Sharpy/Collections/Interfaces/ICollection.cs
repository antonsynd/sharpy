namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for sized iterable container classes.
/// </summary>
public interface ICollection<T> : IContainer<T>, IIterable<T>, ISized
{
}
