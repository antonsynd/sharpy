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
            var memo = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
            return DeepCopyInternal(x, memo);
        }

        private static object DeepCopyInternal(object x, Dictionary<object, object> memo)
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

            // Sharpy List<T>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return DeepCopyList(x, type, memo);
            }

            // Sharpy Dict<K,V>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dict<,>))
            {
                return DeepCopyDict(x, type, memo);
            }

            // Sharpy Set<T>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Set<>))
            {
                return DeepCopySet(x, type, memo);
            }

            // Fallback: shallow copy for unknown reference types
            return Copy(x);
        }

        private static object DeepCopyList(object x, Type type, Dictionary<object, object> memo)
        {
            // Create empty list of the same type
            // TODO(#403): Replace reflection-based deep copy with interface-based approach
            object newList = Activator.CreateInstance(type)!;
            memo[x] = newList;

            // Get the IEnumerable<T> interface to iterate
            MethodInfo? appendMethod = type.GetMethod("Append");
            if (appendMethod == null)
            {
                return newList;
            }

            try
            {
                foreach (object? item in (System.Collections.IEnumerable)x)
                {
                    object? copiedItem = item != null ? DeepCopyInternal(item, memo) : null;
                    appendMethod.Invoke(newList, new[] { copiedItem });
                }
            }
            catch (TargetInvocationException ex)
            {
                throw new TypeError($"copy.deepcopy() failed for list element: {ex.InnerException?.Message ?? ex.Message}");
            }

            return newList;
        }

        private static object DeepCopyDict(object x, Type type, Dictionary<object, object> memo)
        {
            // TODO(#403): Replace reflection-based deep copy with interface-based approach
            object newDict = Activator.CreateInstance(type)!;
            memo[x] = newDict;

            Type[] genericArgs = type.GetGenericArguments();
            MethodInfo addMethod = type.GetMethod("Add", genericArgs)!;

            // Use the generic IEnumerable<KeyValuePair<K,V>> to iterate
            Type kvpType = typeof(KeyValuePair<,>).MakeGenericType(genericArgs);
            Type enumerableType = typeof(IEnumerable<>).MakeGenericType(kvpType);
            MethodInfo getEnumerator = enumerableType.GetMethod("GetEnumerator")!;
            Type enumeratorType = typeof(IEnumerator<>).MakeGenericType(kvpType);
            PropertyInfo currentProp = enumeratorType.GetProperty("Current")!;
            PropertyInfo keyProp = kvpType.GetProperty("Key")!;
            PropertyInfo valueProp = kvpType.GetProperty("Value")!;

            object enumerator = getEnumerator.Invoke(x, null)!;
            MethodInfo moveNext = typeof(System.Collections.IEnumerator).GetMethod("MoveNext")!;

            try
            {
                while ((bool)moveNext.Invoke(enumerator, null)!)
                {
                    object kvp = currentProp.GetValue(enumerator)!;
                    object key = keyProp.GetValue(kvp)!;
                    object? value = valueProp.GetValue(kvp);

                    object copiedKey = DeepCopyInternal(key, memo);
                    object? copiedValue = value != null ? DeepCopyInternal(value, memo) : null;

                    try
                    {
                        addMethod.Invoke(newDict, new[] { copiedKey, copiedValue });
                    }
                    catch (TargetInvocationException ex)
                    {
                        throw new TypeError($"copy.deepcopy() failed for dict entry: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }

            return newDict;
        }

        private static object DeepCopySet(object x, Type type, Dictionary<object, object> memo)
        {
            object newSet = Activator.CreateInstance(type)!;
            memo[x] = newSet;

            MethodInfo? addMethod = type.GetMethod("Add", type.GetGenericArguments());
            if (addMethod == null)
            {
                return newSet;
            }

            foreach (object? item in (System.Collections.IEnumerable)x)
            {
                object? copiedItem = item != null ? DeepCopyInternal(item, memo) : null;
                addMethod.Invoke(newSet, new[] { copiedItem });
            }

            return newSet;
        }
    }

    /// <summary>
    /// An equality comparer that uses reference equality (object identity).
    /// Used internally by <see cref="CopyModule"/> to track circular references
    /// during deep copy.
    /// </summary>
    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        private ReferenceEqualityComparer() { }

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
