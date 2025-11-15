namespace Sharpy.Core;

/// <summary>
/// An interface for a type that supports unary plus operation.
/// Implements the __pos__ dunder method.
/// </summary>
/// <typeparam name="T">The type that supports unary plus.</typeparam>
public interface IUnaryPlusable<T>
    where T : IUnaryPlusable<T>
{
    /// <summary>
    /// Implements the __pos__ dunder method for unary plus operation.
    /// Maps to the + operator in Sharpy code.
    /// </summary>
    /// <returns>The positive value (typically returns self).</returns>
    T __Pos__();

    static virtual T operator +(T value)
    {
        if (value is null)
        {
            throw TypeError.OpNotSupported("+", "NoneType");
        }

        return value.__Pos__();
    }
}
