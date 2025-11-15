namespace Sharpy.Core;

/// <summary>
/// An interface for a type that can be negated (unary minus).
/// Implements the __neg__ dunder method.
/// </summary>
/// <typeparam name="T">The type that can be negated.</typeparam>
public interface INegatable<T>
    where T : INegatable<T>
{
    /// <summary>
    /// Implements the __neg__ dunder method for unary minus operation.
    /// Maps to the - operator in Sharpy code.
    /// </summary>
    /// <returns>The negated value.</returns>
    T __Neg__();

    static virtual T operator -(T value)
    {
        if (value is null)
        {
            throw TypeError.OpNotSupported("-", "NoneType");
        }

        return value.__Neg__();
    }
}
