namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for sized iterable container classes.
/// </summary>
public interface ICollection<T>
    : IContainer<T>,
      IIterable<T>,
      ISized,
      System.Collections.Generic.ICollection<T>
{
    int System.Collections.Generic.ICollection<T>.Count
    {
        get
        {
            return (int)__Len__();
        }
    }

    int Count
    {
        get
        {
            return (int)__Len__();
        }
    }
}
