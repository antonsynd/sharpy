using System;
using System.Collections;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// A readonly struct wrapping <see cref="string"/> that provides Python-compatible
    /// string methods as instance methods. Follows the same pattern as
    /// <see cref="List{T}"/>, <see cref="Dict{K,V}"/>, and <see cref="Set{T}"/>.
    /// </summary>
    public readonly partial struct Str
        : IEquatable<Str>,
          IComparable<Str>,
          IEnumerable<Str>,
          ISized,
          IBoolConvertible
    {
        internal readonly string Value;

        /// <summary>
        /// Constructs a Str from a .NET string. Null is coalesced to empty string.
        /// </summary>
        public Str(string value)
        {
            Value = value ?? "";
        }

        /// <summary>
        /// Implicit conversion from <see cref="string"/> to <see cref="Str"/>.
        /// </summary>
        public static implicit operator Str(string s) => new Str(s);

        /// <summary>
        /// Implicit conversion from <see cref="Str"/> to <see cref="string"/>.
        /// </summary>
        public static implicit operator string(Str s) => s.Value;

        /// <summary>
        /// Gets the number of UTF-16 code units in the string (for len() dispatch).
        /// </summary>
        int ISized.Count => Value.Length;

        /// <summary>
        /// Returns true if the string is non-empty (for bool() dispatch).
        /// </summary>
        bool IBoolConvertible.IsTrue => Value.Length > 0;

        /// <inheritdoc/>
        public override string ToString() => Value;

        /// <inheritdoc/>
        public override int GetHashCode() => Value.GetHashCode();

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is Str other)
            {
                return string.Equals(Value, other.Value, StringComparison.Ordinal);
            }
            if (obj is string s)
            {
                return string.Equals(Value, s, StringComparison.Ordinal);
            }
            return false;
        }

        /// <summary>
        /// Determines whether this Str equals another Str (ordinal comparison).
        /// </summary>
        public bool Equals(Str other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// Compares this Str to another Str (ordinal comparison).
        /// </summary>
        public int CompareTo(Str other)
        {
            return string.Compare(Value, other.Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns an enumerator that yields single-character Str values.
        /// </summary>
        public IEnumerator<Str> GetEnumerator()
        {
            for (int i = 0; i < Value.Length; i++)
            {
                yield return new Str(Value[i].ToString());
            }
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
