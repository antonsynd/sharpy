namespace Sharpy.Core;

/// <summary>
/// An interface for a type that supports bitwise NOT operation.
/// Implements the __invert__ dunder method.
/// </summary>
/// <typeparam name="T">The type that supports bitwise NOT.</typeparam>
public interface IInvertible<T>
    where T : IInvertible<T>
{
    /// <summary>
    /// Implements the __invert__ dunder method for bitwise NOT operation.
    /// Maps to the ~ operator in Sharpy code.
    /// </summary>
    /// <returns>The bitwise inverted value.</returns>
    T __Invert__();

    static virtual T operator ~(T value)
    {
        if (value is null)
        {
            throw TypeError.OpNotSupported("~", "NoneType");
        }

        return value.__Invert__();
    }
}
