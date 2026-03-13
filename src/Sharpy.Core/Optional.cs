using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sharpy
{
    /// <summary>
    /// A safe tagged union for optional values. T? desugars to Optional[T].
    /// This is a struct - no heap allocation for returning optional values.
    /// </summary>
    public readonly struct Optional<T> : System.IEquatable<Optional<T>>
    {
        private readonly T _value;
        private readonly bool _hasValue;

        private Optional(T value, bool hasValue)
        {
            _value = value;
            _hasValue = hasValue;
        }

        /// <summary>Create an Optional containing the given value.</summary>
        public static Optional<T> Some(T value) => new Optional<T>(value, true);
        /// <summary>Get an empty Optional.</summary>
        public static Optional<T> None => new Optional<T>(default!, false);

        /// <summary>Returns true if this Optional contains a value.</summary>
        public bool IsSome => _hasValue;
        /// <summary>Returns true if this Optional is empty.</summary>
        public bool IsNone => !_hasValue;

        /// <summary>Returns the contained value, or throws if empty.</summary>
        public T Unwrap() =>
            _hasValue ? _value : throw new InvalidOperationException("Called Unwrap on empty Optional");

        /// <summary>Returns the contained value, or the provided default.</summary>
        public T UnwrapOr(T defaultValue) =>
            _hasValue ? _value : defaultValue;

        /// <summary>Returns the contained value, or computes it from a function.</summary>
        public T UnwrapOrElse(Func<T> f) =>
            _hasValue ? _value : f();

        /// <summary>Maps the contained value using the given function, or returns None if empty.</summary>
        public Optional<U> Map<U>(Func<T, U> f) =>
            _hasValue ? Optional<U>.Some(f(_value)) : Optional<U>.None;

        /// <summary>Deconstructs the optional into its components for pattern matching.</summary>
        public void Deconstruct(out bool hasValue, out T value)
        {
            hasValue = _hasValue;
            value = _value;
        }

        /// <summary>Returns a string representation of the optional.</summary>
        public override string ToString() =>
            _hasValue ? $"Some({_value})" : "None";

        /// <summary>Determines whether this optional is equal to the specified object.</summary>
        public override bool Equals(object? obj) =>
            obj is Optional<T> other && Equals(other);

        /// <summary>Determines whether this optional is equal to another optional.</summary>
        public bool Equals(Optional<T> other) =>
            _hasValue == other._hasValue &&
            (!_hasValue || EqualityComparer<T>.Default.Equals(_value, other._value));

        /// <summary>Returns a hash code for the optional.</summary>
        public override int GetHashCode()
        {
            if (!_hasValue)
                return 0;
            unchecked
            {
                return 31 * _hasValue.GetHashCode() + EqualityComparer<T>.Default.GetHashCode(_value!);
            }
        }

        /// <summary>Determines whether two optionals are equal.</summary>
        public static bool operator ==(Optional<T> left, Optional<T> right) => left.Equals(right);
        /// <summary>Determines whether two optionals are not equal.</summary>
        public static bool operator !=(Optional<T> left, Optional<T> right) => !left.Equals(right);

        /// <summary>
        /// Implicit conversion from T to Optional&lt;T&gt;.
        /// For value types, always produces Some(value) since value types cannot be null.
        /// For reference types, null produces None, non-null produces Some(value).
        /// </summary>
        public static implicit operator Optional<T>(T value) =>
            value is null ? None : Some(value);
    }

    /// <summary>
    /// Static factory methods for Optional. Enables Some(value) syntax with type inference.
    /// </summary>
    public static class Optional
    {
        private static readonly Type OptionalGenericDef = typeof(Optional<>);

        // Cached reflection accessors per concrete Optional<T> type to avoid
        // repeated GetProperty/GetField lookups on every TryFormat call.
        private static readonly Dictionary<Type, (PropertyInfo isSome, FieldInfo value)> ReflectionCache
            = new Dictionary<Type, (PropertyInfo, FieldInfo)>();

        /// <summary>Create an Optional containing the given value.</summary>
        public static Optional<T> Some<T>(T value) => Optional<T>.Some(value);

        /// <summary>
        /// Converts a nullable reference type to Optional. Used by the 'maybe' expression.
        /// </summary>
        public static Optional<T> From<T>(T? value) where T : class
            => value is null ? Optional<T>.None : Optional<T>.Some(value);

        /// <summary>
        /// Converts a nullable value type to Optional. Used by the 'maybe' expression.
        /// </summary>
        public static Optional<T> From<T>(T? value) where T : struct
            => value.HasValue ? Optional<T>.Some(value.Value) : Optional<T>.None;

        private static (PropertyInfo isSome, FieldInfo value) GetAccessors(Type type)
        {
            if (!ReflectionCache.TryGetValue(type, out var accessors))
            {
                accessors = (
                    type.GetProperty("IsSome"),
                    type.GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance)
                );
                ReflectionCache[type] = accessors;
            }
            return accessors;
        }

        /// <summary>
        /// Detect and format an Optional value at runtime using cached reflection.
        /// Avoids the boxing caused by the former IOptional interface approach by
        /// caching PropertyInfo/FieldInfo per concrete Optional&lt;T&gt; type.
        /// </summary>
        /// <param name="obj">A boxed object that may be an Optional&lt;T&gt;.</param>
        /// <param name="result">The formatted string if obj is an Optional.</param>
        /// <returns>True if obj was an Optional and was formatted; false otherwise.</returns>
        internal static bool TryFormat(object obj, out string result)
        {
            var type = obj.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == OptionalGenericDef)
            {
                var (isSomeProp, valueField) = GetAccessors(type);
                var hasValue = (bool)isSomeProp.GetValue(obj);
                if (!hasValue)
                {
                    result = "None";
                    return true;
                }

                var innerValue = valueField.GetValue(obj);
                result = Builtins.Str(innerValue!);
                return true;
            }

            result = null!;
            return false;
        }
    }
}
