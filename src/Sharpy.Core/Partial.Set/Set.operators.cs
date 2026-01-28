namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <remarks>
    /// This returns true for both sets if they contain the same items,
    /// even if they are not the actual same set reference.
    /// </remarks>
    public static bool operator ==(Set<T>? left, Set<T>? right)
    {
        return left?.Equals(right) ?? right is null;
    }

    public static bool operator !=(Set<T>? left, Set<T>? right)
    {
        return !(left == right);
    }

    public static bool operator <(Set<T> left, Set<T> right)
    {
        return left.__Lt__(right);
    }

    public static bool operator >(Set<T> left, Set<T> right)
    {
        return left.__Gt__(right);
    }

    public static bool operator <=(Set<T> left, Set<T> right)
    {
        return left.__Le__(right);
    }

    public static bool operator >=(Set<T> left, Set<T> right)
    {
        return left.__Ge__(right);
    }

    public static Set<T> operator |(Set<T> left, Set<T> right)
    {
        return left.__Or__(right);
    }

    public static Set<T> operator &(Set<T> left, Set<T> right)
    {
        return left.__And__(right);
    }

    public static Set<T> operator ^(Set<T> left, Set<T> right)
    {
        return left.__XOr__(right);
    }

    public static Set<T> operator -(Set<T> left, Set<T> right)
    {
        return left.__Sub__(right);
    }

    public static bool operator true(Set<T>? set)
    {
        return set is not null && set._set.Count > 0;
    }

    public static bool operator false(Set<T>? set)
    {
        return set is null || set._set.Count == 0;
    }
}
