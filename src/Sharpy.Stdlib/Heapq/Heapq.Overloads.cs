using System;
using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Heapq
    {
        /// <summary>Find the n largest elements in a dataset, accepting any IList.</summary>
        public static Sharpy.List<T> Nlargest<T>(int n, IList<T> iterable) where T : IComparable<T>
            => Nlargest(n, new Sharpy.List<T>(iterable));

        /// <summary>Find the n smallest elements in a dataset, accepting any IList.</summary>
        public static Sharpy.List<T> Nsmallest<T>(int n, IList<T> iterable) where T : IComparable<T>
            => Nsmallest(n, new Sharpy.List<T>(iterable));
    }
}
