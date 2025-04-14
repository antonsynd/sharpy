namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for classes that provide the <see cref="__Len__()"/> method.
/// </summary>
public interface ISized
{
    /// <summary>
    /// Return the length (the number of items) of an object.
    /// </summary>
    uint __Len__();

    int Length
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
