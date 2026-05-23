using System;
using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Heapq
    {
        public static Sharpy.List<T> Nlargest<T>(int n, IList<T> iterable) where T : IComparable<T>
            => Nlargest(n, new Sharpy.List<T>(iterable));

        public static Sharpy.List<T> Nsmallest<T>(int n, IList<T> iterable) where T : IComparable<T>
            => Nsmallest(n, new Sharpy.List<T>(iterable));
    }
}
