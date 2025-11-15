namespace Sharpy.Core;

/// <summary>
/// An interface for a type that supports bitwise XOR operation.
/// Implements the __xor__ dunder method.
/// </summary>
/// <typeparam name="T">The type that supports bitwise XOR.</typeparam>
public interface IBitwiseXorable<T>
    where T : IBitwiseXorable<T>
{
    /// <summary>
    /// Implements the __xor__ dunder method for bitwise XOR operation.
    /// Maps to the ^ operator in Sharpy code.
    /// </summary>
    /// <param name="other">The right operand.</param>
    /// <returns>The result of bitwise XOR.</returns>
    T __Xor__(T other);

    static virtual T operator ^(T left, T right)
    {
        if (left is null || right is null)
        {
            throw TypeError.OpNotSupported("^", "NoneType");
        }

        return left.__Xor__(right);
    }
}
