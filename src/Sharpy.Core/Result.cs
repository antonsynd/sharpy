using System;
using System.Collections.Generic;

namespace Sharpy.Core
{
    /// <summary>
    /// A safe tagged union for error handling. T !E desugars to Result[T, E].
    /// This is a struct - no heap allocation for returning result values.
    /// </summary>
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

        // Factory methods
        public static Result<T, E> Ok(T value) => new Result<T, E>(value, default!, true);
        public static Result<T, E> Err(E error) => new Result<T, E>(default!, error, false);

        // Properties
        public bool IsOk => _isOk;
        public bool IsErr => !_isOk;

        // Methods
        public T Unwrap() =>
            _isOk ? _value : throw new InvalidOperationException($"Called Unwrap on Err: {_error}");

        public T UnwrapOr(T defaultValue) =>
            _isOk ? _value : defaultValue;

        public T UnwrapOrElse(Func<E, T> f) =>
            _isOk ? _value : f(_error);

        public E UnwrapErr() =>
            _isOk ? throw new InvalidOperationException($"Called UnwrapErr on Ok: {_value}") : _error;

        public Result<U, E> Map<U>(Func<T, U> f) =>
            _isOk ? Result<U, E>.Ok(f(_value)) : Result<U, E>.Err(_error);

        public Result<T, F> MapErr<F>(Func<E, F> f) =>
            _isOk ? Result<T, F>.Ok(_value) : Result<T, F>.Err(f(_error));

        // For pattern matching support (future)
        public void Deconstruct(out bool isOk, out T value, out E error)
        {
            isOk = _isOk;
            value = _value;
            error = _error;
        }

        public override string ToString() =>
            _isOk ? $"Ok({_value})" : $"Err({_error})";

        public override bool Equals(object? obj) =>
            obj is Result<T, E> other && Equals(other);

        public bool Equals(Result<T, E> other) =>
            _isOk == other._isOk &&
            (_isOk
                ? EqualityComparer<T>.Default.Equals(_value, other._value)
                : EqualityComparer<E>.Default.Equals(_error, other._error));

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

        public static bool operator ==(Result<T, E> left, Result<T, E> right) => left.Equals(right);
        public static bool operator !=(Result<T, E> left, Result<T, E> right) => !left.Equals(right);
    }

    /// <summary>
    /// Static factory methods for Result. Enables Ok(value)/Err(error) syntax with type inference.
    /// </summary>
    public static class Result
    {
        public static Result<T, E> Ok<T, E>(T value) => Result<T, E>.Ok(value);
        public static Result<T, E> Err<T, E>(E error) => Result<T, E>.Err(error);
    }
}
