namespace Sharpy;

public partial class Object
{
    /// <remarks>
    /// Sealed to prevent subclasses from overriding this mapping to
    /// <see cref="__Eq__(Object)"/> which should be the one that subclasses
    /// override.
    /// </remarks>
    public override sealed bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        return __Eq__(obj);
    }

    /// <remarks>
    /// Sealed to prevent subclasses from overriding this mapping to
    /// <see cref="__Hash__()"/> which should be the one that subclasses
    /// override.
    /// </remarks>
    public override sealed int GetHashCode()
    {
        return __Hash__();
    }

    /// <remarks>
    /// Sealed to prevent subclasses from overriding this mapping to
    /// <see cref="__Str__()"/> which should be the one that subclasses
    /// override.
    /// </remarks>
    public override sealed string ToString()
    {
        return __Str__();
    }
}
