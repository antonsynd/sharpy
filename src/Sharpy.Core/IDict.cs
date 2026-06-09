using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Non-generic Pythonic dict protocol interface implemented by every
    /// <see cref="Dict{K, V}"/> instantiation via explicit boxing adapters.
    /// <para>
    /// Used as the binding type when pattern-matching a bare <c>case dict() as d:</c>
    /// against an <c>object</c> scrutinee. The type-erased surface lets the emitter's
    /// name-mangled member access resolve structurally (e.g. <c>d.Items()</c>,
    /// <c>d[key]</c>) without special-casing every dict member in codegen.
    /// </para>
    /// <para>
    /// <b>Type-erasure semantics (Axiom 1 — .NET first):</b> reads with a wrong-typed
    /// key behave as if the key is absent (KeyError / false / null); writes cast to the
    /// underlying generic parameters and let .NET throw on mismatch.
    /// </para>
    /// </summary>
    public interface IDict : ISized, IEnumerable<object?>
    {
        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// Get throws <see cref="KeyError"/> if the key is absent or wrong-typed.
        /// Set casts key/value to the underlying generic types.
        /// </summary>
        object? this[object key] { get; set; }

        /// <summary>Return an enumerable of (key, value) tuples.</summary>
        IEnumerable<(object?, object?)> Items();

        /// <summary>Return an enumerable of keys.</summary>
        IEnumerable<object?> Keys();

        /// <summary>Return an enumerable of values.</summary>
        IEnumerable<object?> Values();

        /// <summary>Check if <paramref name="key"/> exists in the dictionary.</summary>
        bool Contains(object key);

        /// <summary>
        /// Return the value for <paramref name="key"/> if present, otherwise
        /// <see cref="Optional{T}.None"/>.
        /// </summary>
        Optional<object?> Get(object key);

        /// <summary>
        /// Return the value for <paramref name="key"/> if present, otherwise
        /// <paramref name="defaultValue"/>.
        /// </summary>
        object? Get(object key, object? defaultValue);

        /// <summary>
        /// Remove the specified key and return its value.
        /// Throws <see cref="KeyError"/> if the key is absent.
        /// </summary>
        object? Pop(object key);

        /// <summary>
        /// Remove the specified key and return its value, or
        /// <paramref name="defaultValue"/> if absent.
        /// </summary>
        object? Pop(object key, object? defaultValue);

        /// <summary>
        /// Remove and return a (key, value) pair.
        /// </summary>
        (object?, object?) PopItem(bool last = false);

        /// <summary>
        /// If <paramref name="key"/> is present, return its value.
        /// Otherwise insert it with <paramref name="defaultValue"/> and return that.
        /// </summary>
        object? SetDefault(object key, object? defaultValue);

        /// <summary>Remove all items from the dictionary.</summary>
        void Clear();

        /// <summary>Return a shallow copy of the dictionary.</summary>
        IDict Copy();

        /// <summary>
        /// Update the dictionary with key-value pairs from <paramref name="other"/>.
        /// </summary>
        void Update(IDict other);

        /// <summary>
        /// Update the dictionary with key-value pairs from an iterable of tuples.
        /// </summary>
        void Update(IEnumerable<(object?, object?)> other);

        /// <summary>
        /// Remove the item with the specified key.
        /// Throws <see cref="KeyError"/> if the key is absent.
        /// </summary>
        void Remove(object key);
    }
}
