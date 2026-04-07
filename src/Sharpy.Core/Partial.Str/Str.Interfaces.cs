using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Containment and reverse iteration for Str.
    /// </summary>
    public readonly partial struct Str : IReverseEnumerable<Str>
    {
        /// <summary>
        /// Return true if <paramref name="substring"/> is found within this string.
        /// Used for <c>"x" in s</c> codegen.
        /// </summary>
        public bool Contains(Str substring)
        {
            return Value.IndexOf((string)substring, StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the string in reverse order,
        /// yielding single-character Str values.
        /// </summary>
        public IEnumerator<Str> GetReverseEnumerator()
        {
            for (int i = Value.Length - 1; i >= 0; i--)
            {
                yield return new Str(Value[i].ToString());
            }
        }
    }
}
