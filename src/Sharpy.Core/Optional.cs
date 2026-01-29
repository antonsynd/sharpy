using System;
using System.Collections.Generic;

namespace Sharpy.Core
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
        public static Optional<T> Nothing => new Optional<T>(default!, false);

        // Properties
        public bool IsSome => _hasValue;
        public bool IsNothing => !_hasValue;

        // Methods
        public T Unwrap() =>
            _hasValue ? _value : throw new InvalidOperationException("Called Unwrap on Nothing");

        public T UnwrapOr(T defaultValue) =>
            _hasValue ? _value : defaultValue;

        public T UnwrapOrElse(Func<T> f) =>
            _hasValue ? _value : f();

        public Optional<U> Map<U>(Func<T, U> f) =>
            _hasValue ? Optional<U>.Some(f(_value)) : Optional<U>.Nothing;

        // For pattern matching support (future)
        public void Deconstruct(out bool hasValue, out T value)
        {
            hasValue = _hasValue;
            value = _value;
        }

        public override string ToString() =>
            _hasValue ? $"Some({_value})" : "Nothing";

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
    }

    /// <summary>
    /// Static factory methods for Optional. Enables Some(value) syntax with type inference.
    /// </summary>
    public static class Optional
    {
        public static Optional<T> Some<T>(T value) => Optional<T>.Some(value);

        /// <summary>
        /// Converts a nullable reference type to Optional. Used by the 'maybe' expression.
        /// </summary>
        public static Optional<T> From<T>(T? value) where T : class
            => value is null ? Optional<T>.Nothing : Optional<T>.Some(value);

        /// <summary>
        /// Converts a nullable value type to Optional. Used by the 'maybe' expression.
        /// </summary>
        public static Optional<T> From<T>(T? value) where T : struct
            => value.HasValue ? Optional<T>.Some(value.Value) : Optional<T>.Nothing;
    }
}
