using System.Diagnostics.CodeAnalysis;

namespace Sharpy;

internal static class EqualityComparerAdapter<T>
{
    public static readonly IEqualityComparer<T> Instance = CreateComparer();

    private static IEqualityComparer<T> CreateComparer()
    {
        if (typeof(T).IsAssignableTo(typeof(IEquatable<T>)))
        {
            return new EquatableEqualityComparer();
        }

        if (typeof(T).IsSubclassOf(typeof(Object)))
        {
            return new ObjectEqualityComparer();
        }

        return EqualityComparer<T>.Default;
    }

    private class EquatableEqualityComparer : IEqualityComparer<T>
    {
        public bool Equals(T? x, T? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return ((IEquatable<T>)x).__Eq__(y);
        }

        public int GetHashCode([DisallowNull] T obj)
        {
            if (obj is null)
            {
                throw new TypeError("'NoneType' is not hashable");
            }

            return ((IEquatable<T>)obj).__Hash__();
        }
    }

    private class ObjectEqualityComparer : IEqualityComparer<T>
    {
        public bool Equals(T? x, T? y)
        {
            var xObj = x as Object;
            var yObj = y as Object;

            if (ReferenceEquals(xObj, yObj))
            {
                return true;
            }

            if (xObj is null || yObj is null)
            {
                return false;
            }

            return xObj.__Eq__(yObj);
        }

        public int GetHashCode([DisallowNull] T obj)
        {
            if (obj is Object o)
            {
                return o.__Hash__();
            }

            throw new TypeError("'NoneType' is not hashable");
        }
    }
}
