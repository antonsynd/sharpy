namespace Sharpy.Core;

/// <summary>
/// An interface for a type that supports bitwise OR operation.
/// Implements the __or__ dunder method.
/// </summary>
/// <typeparam name="T">The type that supports bitwise OR.</typeparam>
public interface IBitwiseOrable<T>
    where T : IBitwiseOrable<T>
{
    /// <summary>
    /// Implements the __or__ dunder method for bitwise OR operation.
    /// Maps to the | operator in Sharpy code.
    /// </summary>
    /// <param name="other">The right operand.</param>
    /// <returns>The result of bitwise OR.</returns>
    T __Or__(T other);

    static virtual T operator |(T left, T right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("|", "NoneType");
        }

        return left.__Or__(right);
    }
}
