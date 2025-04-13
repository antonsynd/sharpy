namespace Sharpy.Operator;

public static partial class Exports
{
    /// <summary>
    /// Compares two <see cref="IEquatable&lt;T&gt;"/> objects for equality.
    /// </summary>
    /// <param name="left">The left object.</param>
    /// <param name="right">The right object.</param>
    /// <returns>True if the objects are equal, false otherwise.</returns>
    /// <typeparam name="T">The type of the objects being compared.</typeparam>
    public static bool Eq<T>(T left, T right) where T : IEquatable<T>
    {
        return left?.__Eq__(right) ?? right is null;
    }

    public static bool Eq(object left, object right)
    {
        if (left is Object objLeft)
        {
            if (right is Object objRight)
            {
                // Delegate to generic version above
                return Eq(objLeft, objRight);
            }

            return objLeft.__Eq__(right);
        }
        else if (right is Object objRight)
        {
            return objRight.__Eq__(left);
        }

        return left?.Equals(right) ?? right is null;
    }

    public static bool __Eq__<T>(T left, T right) where T : IEquatable<T> => Eq(left, right);
    public static bool __Eq__(object left, object right) => Eq(left, right);
}
