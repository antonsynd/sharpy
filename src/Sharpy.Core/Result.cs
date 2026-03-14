using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// A safe tagged union for error handling. <c>T !E</c> desugars to <c>Result&lt;T, E&gt;</c>.
    /// This is a struct — no heap allocation for returning result values.
    /// </summary>
    /// <remarks>
    /// Use <c>Result&lt;T, E&gt;</c> in your own APIs for explicit, type-safe error handling.
    /// The Sharpy stdlib uses Python-style exceptions for familiarity. <c>Result</c> is most
    /// useful when you want callers to handle the error case at compile time rather than relying
    /// on try/catch. Use the <c>try</c> expression to wrap throwing code into a <c>Result</c>.
    /// </remarks>
    public readonly struct Result<T, E> : System.IEquatable<Result<T, E>>
    {
        private readonly T _value;
        private readonly E _error;
        private readonly bool _isOk;

        private Result(T value, E error, bool isOk)
        {
            _value = value;
            _error = error;
            _isOk = isOk;
        }

        /// <summary>Create an Ok result containing the given value.</summary>
        public static Result<T, E> Ok(T value) => new Result<T, E>(value, default!, true);
        /// <summary>Create an Err result containing the given error.</summary>
        public static Result<T, E> Err(E error) => new Result<T, E>(default!, error, false);

        /// <summary>Returns true if this result is Ok.</summary>
        public bool IsOk => _isOk;
        /// <summary>Returns true if this result is Err.</summary>
        public bool IsErr => !_isOk;

        /// <summary>Returns the contained Ok value, or throws if Err.</summary>
        public T Unwrap() =>
            _isOk ? _value : throw new InvalidOperationException($"Called Unwrap on Err: {_error}");

        /// <summary>Returns the contained Ok value, or the provided default.</summary>
        public T UnwrapOr(T defaultValue) =>
            _isOk ? _value : defaultValue;

        /// <summary>Returns the contained Ok value, or computes it from a function applied to the error.</summary>
        public T UnwrapOrElse(Func<E, T> f) =>
            _isOk ? _value : f(_error);

        /// <summary>Returns the contained Err value, or throws if Ok.</summary>
        public E UnwrapErr() =>
            _isOk ? throw new InvalidOperationException($"Called UnwrapErr on Ok: {_value}") : _error;

        /// <summary>Maps an Ok value using the given function, leaving Err unchanged.</summary>
        public Result<U, E> Map<U>(Func<T, U> f) =>
            _isOk ? Result<U, E>.Ok(f(_value)) : Result<U, E>.Err(_error);

        /// <summary>Maps an Err value using the given function, leaving Ok unchanged.</summary>
        public Result<T, F> MapErr<F>(Func<E, F> f) =>
            _isOk ? Result<T, F>.Ok(_value) : Result<T, F>.Err(f(_error));

        /// <summary>Deconstructs the result into its components for pattern matching.</summary>
        public void Deconstruct(out bool isOk, out T value, out E error)
        {
            isOk = _isOk;
            value = _value;
            error = _error;
        }

        /// <summary>Returns a string representation of the result.</summary>
        public override string ToString() =>
            _isOk ? $"Ok({_value})" : $"Err({_error})";

        /// <summary>Determines whether this result is equal to the specified object.</summary>
        public override bool Equals(object? obj) =>
            obj is Result<T, E> other && Equals(other);

        /// <summary>Determines whether this result is equal to another result.</summary>
        public bool Equals(Result<T, E> other) =>
            _isOk == other._isOk &&
            (_isOk
                ? EqualityComparer<T>.Default.Equals(_value, other._value)
                : EqualityComparer<E>.Default.Equals(_error, other._error));

        /// <summary>Returns a hash code for the result.</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                if (_isOk)
                    return 31 * true.GetHashCode() + EqualityComparer<T>.Default.GetHashCode(_value!);
                else
                    return 31 * false.GetHashCode() + EqualityComparer<E>.Default.GetHashCode(_error!);
            }
        }

        /// <summary>Determines whether two results are equal.</summary>
        public static bool operator ==(Result<T, E> left, Result<T, E> right) => left.Equals(right);
        /// <summary>Determines whether two results are not equal.</summary>
        public static bool operator !=(Result<T, E> left, Result<T, E> right) => !left.Equals(right);
    }

    /// <summary>
    /// Static factory methods for Result. Enables Ok(value)/Err(error) syntax with type inference.
    /// Also provides Try() for wrapping expressions that may throw.
    /// </summary>
    public static class Result
    {
        /// <summary>Create an Ok result containing the given value.</summary>
        public static Result<T, E> Ok<T, E>(T value) => Result<T, E>.Ok(value);
        /// <summary>Create an Err result containing the given error.</summary>
        public static Result<T, E> Err<T, E>(E error) => Result<T, E>.Err(error);

        /// <summary>
        /// Wraps a function call in a try/catch, returning Ok on success or Err on exception.
        /// Used by the 'try' expression: try expr → Result[T, Exception].
        /// </summary>
        public static Result<T, Exception> Try<T>(Func<T> func)
        {
            try
            {
                return Result<T, Exception>.Ok(func());
            }
            catch (Exception ex)
            {
                return Result<T, Exception>.Err(ex);
            }
        }

        /// <summary>
        /// Wraps a function call in a try/catch for a specific exception type.
        /// Used by the 'try[E]' expression: try[ValueError] expr → Result[T, ValueError].
        /// Other exception types are not caught and propagate normally.
        /// </summary>
        public static Result<T, E> Try<T, E>(Func<T> func) where E : Exception
        {
            try
            {
                return Result<T, E>.Ok(func());
            }
            catch (E ex)
            {
                return Result<T, E>.Err(ex);
            }
        }
    }
}
