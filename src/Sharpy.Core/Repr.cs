namespace Sharpy.Core;

public static partial class Exports
{
    /// <summary>
    /// Return a string containing a printable representation of an object.
    /// </summary>
    /// <remarks>
    /// For objects that implement IRepresentable, this returns the result
    /// of __Repr__(). For other objects, it attempts to return a string
    /// that would yield the same value when written in code.
    /// </remarks>
    public static string Repr(object? x)
    {
        if (x is IRepresentable repr)
        {
            return repr.__Repr__();
        }

        return x?.ToString() ?? "None";
    }
}
