using System;
using System.Collections.Generic;
using System.Collections;

namespace Sharpy
{
    /// <summary>
    /// Global builtin functions available in all Sharpy programs
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Get the length of a collection or string.
        /// This is the fallback overload for dynamically-typed scenarios.
        /// </summary>
        /// <param name="obj">The object to measure</param>
        /// <returns>The number of elements</returns>
        /// <exception cref="TypeError">Thrown when <paramref name="obj"/> is null or has no len()</exception>
        public static int Len(object obj)
        {
            if (obj is null)
            {
                throw TypeError.ArgNone("len", "sized");
            }

            // Fast path for common types
            if (obj is string s)
                return s.Length;
            if (obj is Array arr)
                return arr.Length;
            if (obj is System.Collections.ICollection collection)
                return collection.Count;

            // Check for generic ICollection<T> or IReadOnlyCollection<T> via reflection
            // This handles types like Set<T> that implement ICollection<T> but not non-generic ICollection
            foreach (var iface in obj.GetType().GetInterfaces())
            {
                if (iface.IsGenericType)
                {
                    var genericDef = iface.GetGenericTypeDefinition();
                    if (genericDef == typeof(ICollection<>) || genericDef == typeof(IReadOnlyCollection<>))
                    {
                        var countProp = iface.GetProperty("Count");
                        if (countProp is not null)
                        {
                            return (int)countProp.GetValue(obj)!;
                        }
                    }
                }
            }

            throw new TypeError($"object of type '{obj.GetType().Name}' has no len()");
        }
    }
}
