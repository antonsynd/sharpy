namespace Sharpy.Core;

/// <summary>
/// An interface for a type that supports right shift operation.
/// Implements the __rshift__ dunder method.
/// </summary>
/// <typeparam name="T">The type that supports right shift.</typeparam>
public interface IRightShiftable<T>
    where T : IRightShiftable<T>
{
    /// <summary>
    /// Implements the __rshift__ dunder method for right shift operation.
    /// Maps to the &gt;&gt; operator in Sharpy code.
    /// </summary>
    /// <param name="count">The number of positions to shift.</param>
    /// <returns>The result of right shift.</returns>
    T __RShift__(int count);

    static virtual T operator >>(T left, int count)
    {
        if (left is null)
        {
            throw TypeError.OpNotSupported(">>", "NoneType");
        }

        return left.__RShift__(count);
    }
}
