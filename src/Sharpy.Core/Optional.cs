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

        // Factory methods
        public static Optional<T> Some(T value) => new Optional<T>(value, true);
        public static Optional<T> None => new Optional<T>(default!, false);

        // Properties
        public bool IsSome => _hasValue;
        public bool IsNone => !_hasValue;

        // Methods
        public T Unwrap() =>
            _hasValue ? _value : throw new InvalidOperationException("Called Unwrap on empty Optional");

        public T UnwrapOr(T defaultValue) =>
            _hasValue ? _value : defaultValue;

        public T UnwrapOrElse(Func<T> f) =>
            _hasValue ? _value : f();

        public Optional<U> Map<U>(Func<T, U> f) =>
            _hasValue ? Optional<U>.Some(f(_value)) : Optional<U>.None;

        // For pattern matching support (future)
        public void Deconstruct(out bool hasValue, out T value)
        {
            hasValue = _hasValue;
            value = _value;
        }

        public override string ToString() =>
            _hasValue ? $"Some({_value})" : "None";

        public override bool Equals(object? obj) =>
            obj is Optional<T> other && Equals(other);

        public bool Equals(Optional<T> other) =>
            _hasValue == other._hasValue &&
            (!_hasValue || EqualityComparer<T>.Default.Equals(_value, other._value));

        public override int GetHashCode()
        {
            if (!_hasValue)
                return 0;
            unchecked
            {
                return 31 * _hasValue.GetHashCode() + EqualityComparer<T>.Default.GetHashCode(_value!);
            }
        }

        public static bool operator ==(Optional<T> left, Optional<T> right) => left.Equals(right);
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

        /// <summary>
        /// Detect and format an Optional value at runtime using reflection.
        /// Avoids the boxing caused by the former IOptional interface approach.
        /// </summary>
        /// <param name="obj">A boxed object that may be an Optional&lt;T&gt;.</param>
        /// <param name="result">The formatted string if obj is an Optional.</param>
        /// <returns>True if obj was an Optional and was formatted; false otherwise.</returns>
        internal static bool TryFormat(object obj, out string result)
        {
            var type = obj.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == OptionalGenericDef)
            {
                var hasValue = (bool)type.GetProperty("IsSome").GetValue(obj);
                if (!hasValue)
                {
                    result = "None";
                    return true;
                }

                var innerValue = type.GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
                result = Builtins.Str(innerValue!);
                return true;
            }

            result = null!;
            return false;
        }
    }
}
