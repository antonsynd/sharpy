using Object = Sharpy.Core.Object;

namespace Sharpy.Operator;

public static partial class Exports
{
    /// <summary>
    /// Compares two <see cref="Sharpy.Core.IEquatable&lt;T&gt;"/> objects for equality.
    /// </summary>
    /// <param name="left">The left object.</param>
    /// <param name="right">The right object.</param>
    /// <returns>True if the objects are equal, false otherwise.</returns>
    /// <typeparam name="T">The type of the objects being compared.</typeparam>
    public static bool Eq<T>(T left, T right) where T : Sharpy.Core.IEquatable<T>
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left?.__Eq__(right) ?? right is null;
    }

    public static bool Eq(Object left, Object right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left?.__Eq__(right) ?? right is null;
    }

    public static bool Eq<T>(IComparable<T> left, T right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left?.CompareTo(right) == 0;
    }

    public static bool Eq(IComparable left, object right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left?.CompareTo(right) == 0;
    }

    public static bool Eq(object left, object right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is Object objLeft)
        {
            if (right is Object objRight)
            {
                // Delegate to Object version above
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

    public static bool __Eq__<T>(T left, T right) where T : Sharpy.Core.IEquatable<T> => Eq(left, right);

    public static bool __Eq__(Object left, Object right) => Eq(left, right);

    public static bool __Eq__<T>(IComparable<T> left, T right) => Eq(left, right);

    public static bool __Eq__(IComparable left, object right) => Eq(left, right);

    public static bool __Eq__(object left, object right) => Eq(left, right);
}
