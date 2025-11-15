namespace Sharpy.Core;

/// <summary>
/// An interface for a type that supports left shift operation.
/// Implements the __lshift__ dunder method.
/// </summary>
/// <typeparam name="T">The type that supports left shift.</typeparam>
public interface ILeftShiftable<T>
    where T : ILeftShiftable<T>
{
    /// <summary>
    /// Implements the __lshift__ dunder method for left shift operation.
    /// Maps to the &lt;&lt; operator in Sharpy code.
    /// </summary>
    /// <param name="count">The number of positions to shift.</param>
    /// <returns>The result of left shift.</returns>
    T __LShift__(int count);

    static virtual T operator <<(T left, int count)
    {
        if (left is null)
        {
            throw TypeError.OpNotSupported("<<", "NoneType");
        }

        return left.__LShift__(count);
    }
}
