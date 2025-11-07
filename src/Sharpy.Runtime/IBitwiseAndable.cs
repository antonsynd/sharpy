namespace Sharpy;

/// <summary>
/// An interface for a type that supports bitwise AND operation.
/// Implements the __and__ dunder method.
/// </summary>
/// <typeparam name="T">The type that supports bitwise AND.</typeparam>
public interface IBitwiseAndable<T>
    where T : IBitwiseAndable<T>
{
    /// <summary>
    /// Implements the __and__ dunder method for bitwise AND operation.
    /// Maps to the &amp; operator in Sharpy code.
    /// </summary>
    /// <param name="other">The right operand.</param>
    /// <returns>The result of bitwise AND.</returns>
    T __And__(T other);

    static virtual T operator &(T left, T right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("&", "NoneType");
        }

        return left.__And__(right);
    }
}
