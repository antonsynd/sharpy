using System.Collections.Generic;
using System;

namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for frozenset
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Return a new frozenset object, optionally with elements taken from iterable.
        /// </summary>
        public static FrozenSet<T> FrozenSet<T>(IEnumerable<T> items) => new(items);

        /// <summary>
        /// Return a new empty frozenset object.
        /// </summary>
        public static FrozenSet<T> FrozenSet<T>() => new();
    }
}
