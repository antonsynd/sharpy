using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sharpy
{
    /// <summary>
    /// Shallow and deep copy operations, similar to Python's <c>copy</c> module.
    /// </summary>
    public static partial class CopyModule
    {
        /// <summary>
        /// Return a shallow copy of <paramref name="x"/>.
        /// For Sharpy collections (<see cref="List{T}"/>, <see cref="Dict{K,V}"/>,
        /// <see cref="Set{T}"/>), a new collection is created with the same element
        /// references. For value types the value is returned as-is. For other
        /// reference types, <c>MemberwiseClone</c> is invoked via reflection.
        /// </summary>
        /// <param name="x">The object to copy.</param>
        /// <returns>A shallow copy of <paramref name="x"/>.</returns>
        /// <example>
        /// <code>
        /// a = [1, 2, 3]
        /// b = copy.copy(a)    # new list, same element references
        /// </code>
        /// </example>
        public static object Copy(object x)
        {
            if (x is null)
            {
                return null!;
            }

            Type type = x.GetType();

            // Value types are already copies
            if (type.IsValueType)
            {
                return x;
            }

            // Sharpy List<T>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                MethodInfo? copyMethod = type.GetMethod("Copy", Type.EmptyTypes);
                return copyMethod!.Invoke(x, null)!;
            }

            // Sharpy Dict<K,V>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dict<,>))
            {
                MethodInfo? copyMethod = type.GetMethod("Copy", Type.EmptyTypes);
                return copyMethod!.Invoke(x, null)!;
            }

            // Sharpy Set<T>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Set<>))
            {
                MethodInfo? copyMethod = type.GetMethod("Copy", Type.EmptyTypes);
                return copyMethod!.Invoke(x, null)!;
            }

            // String is immutable, return as-is
            if (x is string)
            {
                return x;
            }

            // Fallback: MemberwiseClone via reflection
            MethodInfo? cloneMethod = type.GetMethod(
                "MemberwiseClone",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (cloneMethod != null)
            {
                return cloneMethod.Invoke(x, null)!;
            }

            return x;
        }

        /// <summary>
        /// Return a deep copy of <paramref name="x"/>.
        /// For Sharpy collections, elements are recursively deep-copied. An identity
        /// dictionary tracks already-copied objects to handle circular references.
        /// For non-collection objects, falls back to a shallow copy.
        /// </summary>
        /// <param name="x">The object to deep-copy.</param>
        /// <returns>A deep copy of <paramref name="x"/>.</returns>
        /// <example>
        /// <code>
        /// a = [[1, 2], [3, 4]]
        /// b = copy.deepcopy(a)
        /// b[0].append(99)
        /// # a is unchanged: [[1, 2], [3, 4]]
        /// </code>
        /// </example>
        public static object Deepcopy(object x)
        {
            var memo = new Dictionary<object, object>(IdentityEqualityComparer.Instance);
            return DeepCopyInternal(x, memo);
        }

        internal static object DeepCopyInternal(object x, Dictionary<object, object> memo)
        {
            if (x is null)
            {
                return null!;
            }

            Type type = x.GetType();

            // Value types and strings are immutable copies
            if (type.IsValueType || x is string)
            {
                return x;
            }

            // Check memo for circular references
            if (memo.TryGetValue(x, out object? existing))
            {
                return existing;
            }

            // Use IDeepCopyable for collections that support it
            if (x is IDeepCopyable copyable)
            {
                return copyable.DeepCopy(memo);
            }

            // Fallback: shallow copy for unknown reference types
            return Copy(x);
        }
    }

    /// <summary>
    /// An equality comparer that uses reference equality (object identity).
    /// Used internally by <see cref="CopyModule"/> to track circular references
    /// during deep copy. Named <c>IdentityEqualityComparer</c> to avoid shadowing
    /// <c>System.Collections.Generic.ReferenceEqualityComparer</c> which exists
    /// in .NET 5+ but is unavailable on netstandard2.x.
    /// </summary>
    internal sealed class IdentityEqualityComparer : IEqualityComparer<object>
    {
        public static readonly IdentityEqualityComparer Instance = new IdentityEqualityComparer();

        private IdentityEqualityComparer() { }

        public new bool Equals(object? x, object? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
