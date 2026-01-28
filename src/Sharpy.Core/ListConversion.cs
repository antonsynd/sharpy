using System.Collections.Generic;
using System;

namespace Sharpy.Core
{
    /// <summary>
    /// Type conversion functions for list
    /// </summary>
    public static partial class Exports
    {
        /// <summary>
        /// Convert IEnumerable to list
        /// </summary>
        public static List<T> List<T>(IEnumerable<T> enumerable)
        {
            return new List<T>(enumerable);
        }

        /// <summary>
        /// Create empty list
        /// </summary>
        public static List<T> List<T>()
        {
            return new List<T>();
        }

        /// <summary>
        /// Convert list to list (copy)
        /// </summary>
        public static List<T> List<T>(List<T> other)
        {
            return other.Copy();
        }
    }
}
