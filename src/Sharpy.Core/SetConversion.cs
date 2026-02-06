using System.Collections.Generic;
using System;

namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for set
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Convert IEnumerable to set
        /// </summary>
        public static Set<T> Set<T>(IEnumerable<T> enumerable)
        {
            return new Set<T>(enumerable);
        }

        /// <summary>
        /// Create empty set
        /// </summary>
        public static Set<T> Set<T>()
        {
            return new Set<T>();
        }

        /// <summary>
        /// Convert set to set (copy)
        /// </summary>
        public static Set<T> Set<T>(Set<T> other)
        {
            return new Set<T>(other);
        }
    }
}
