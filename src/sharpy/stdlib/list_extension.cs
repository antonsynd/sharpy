using System.Collections.Generic;

namespace Sharpy.Stdlib
{

    public static class ListExtensions<T>
    {
        public static void Append(this List<T> list, T x)
        {
            list.Add(x);
        }

        public static void Extend(this List<T> list, IEnumerable<T> iterable)
        {
            list.AddRange(iterable);
        }

        public static void Insert(this List<T> list, T x)
        {

        }
    }
}
