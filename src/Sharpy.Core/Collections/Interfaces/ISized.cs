using Sharpy.Core;

namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for classes that provide the <see cref="__Len__()"/> method.
/// </summary>
public interface ISized
{
    /// <summary>
    /// Return the length (the number of items) of an object.
    /// </summary>
    int __Len__();

    int Length
    {
        get
        {
            return __Len__();
        }
    }

    int Count
    {
        get
        {
            return __Len__();
        }
    }
}
