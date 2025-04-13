namespace Sharpy;

public sealed partial class Set<T>
{
    public static bool operator ==(Set<T> left, Set<T> right)
    {
        return Operator.Exports.Eq(left, right);
    }

    public static bool operator !=(Set<T> left, Set<T> right)
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

    public static bool operator true(Set<T> set)
    {
        return set?.__Bool__() ?? false;
    }

    public static bool operator false(Set<T> set)
    {
        return !(set?.__Bool__() ?? false);
    }
}
