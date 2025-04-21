namespace Sharpy;

public sealed partial class List<T>
{
    /// <remarks>
    /// This returns true for both lists if they contain the same elements,
    /// even if they are not the actual same list reference. If the elements
    /// are Sharpy Objects, then <see cref="Object.__Eq__(Object)"/> is used.
    /// Otherwise, <see cref="object.Equals(object)"/> is used.
    /// </remarks>
    public static bool operator ==(List<T> left, List<T> right)
    {
        return left?.__Eq__(right) ?? right is null;
    }

    public static bool operator !=(List<T> left, List<T> right)
    {
        return !(left == right);
    }

    public static List<T> operator +(List<T> left, List<T> right)
    {
        if (left is null)
        {
            throw new TypeError($"can only concatenate List<${typeof(T).Name}> (not \"NoneType\") to List<${typeof(T).Name}");
        }

        return left.__Add__(right);
    }

    public static List<T> operator *(List<T> left, int i)
    {
        if (left is null)
        {
            throw new TypeError($"can only multiply List<${typeof(T).Name} (not \"NoneType\") with int");
        }

        return left.__Mul__(i);
    }

    public static List<T> operator *(int i, List<T> left)
    {
        if (left is null)
        {
            throw new TypeError($"can only multiply List<${typeof(T).Name} (not \"NoneType\") with int");
        }

        return left.__RMul__(i);
    }

    public static bool operator true(List<T> list)
    {
        return list?.__Bool__() ?? false;
    }

    public static bool operator false(List<T> list)
    {
        return !(list?.__Bool__() ?? false);
    }
}
