using System.Runtime.CompilerServices;

namespace Sharpy
{
    /// <summary>
    /// The single seam for Python-style negative-index normalisation and the cold-path
    /// exception construction of every Core element accessor (#2404).
    /// </summary>
    /// <remarks>
    /// Accessors keep a small fast path (<c>(uint)i &lt; (uint)count</c>) and route the
    /// negative-index wraparound and every exception construction through the
    /// <see cref="MethodImplOptions.NoInlining"/> helpers below, so the accessor body stays
    /// inside the JIT's inline budget. The message texts are part of the
    /// <c>collection-error-message-texts</c> deviation row and must stay byte-identical.
    /// </remarks>
    internal static class Index
    {
        /// <summary>
        /// Returns <paramref name="i"/> normalised against a sequence of length
        /// <paramref name="max"/> for element access: <c>[0, max)</c> is returned as is,
        /// <c>[-max, 0)</c> counts from the end.
        /// </summary>
        /// <exception cref="IndexError">If the index is out of range.</exception>
        internal static int Normalize(int i, int max) => (uint)i < (uint)max ? i : WrapOrThrow(i, max);

        /// <summary>
        /// Returns <paramref name="i"/> normalised for slice or insertion positions: negative
        /// values count from the end, and the result is clamped to <c>[0, max]</c>. Never throws.
        /// </summary>
        internal static int Clamp(int i, int max)
        {
            if (i < 0)
            {
                i = max + i;
            }

            return System.Math.Min(System.Math.Max(i, 0), max);
        }

        /// <summary>
        /// The slow path of <see cref="Normalize(int, int)"/>: returns <c>max + i</c> for
        /// <c>-max &lt;= i &lt; 0</c>, otherwise throws.
        /// </summary>
        /// <exception cref="IndexError">If the index is out of range.</exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int WrapOrThrow(int i, int max)
        {
            if (i < 0 && i >= -max)
            {
                return max + i;
            }

            throw new IndexError($"list index {i} out of range");
        }

        /// <summary>The <c>list index {i} out of range</c> error (no wraparound admitted).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IndexError OutOfRange(int i) => new IndexError($"list index {i} out of range");

        /// <summary>The <c>pop index {i} out of range</c> error.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IndexError PopOutOfRange(int i) => new IndexError($"pop index {i} out of range");

        /// <summary>The <c>pop from empty list</c> error.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IndexError PopFromEmpty() => new IndexError("pop from empty list");

        /// <summary>The <c>array index out of range</c> error.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IndexError ArrayOutOfRange() => new IndexError("array index out of range");

        /// <summary>The <c>string index out of range</c> error.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IndexError StringOutOfRange() => new IndexError("string index out of range");

        /// <summary>The <c>index out of range</c> error raised by <c>bytes</c>.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IndexError BytesOutOfRange() => new IndexError("index out of range");

        /// <summary>The <c>{x} is not in list</c> error raised by <c>list.index</c>.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ValueError NotInList<T>(T x) => new ValueError($"{x} is not in list");

        /// <summary>The <see cref="KeyError"/> for a missing mapping key.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static KeyError KeyNotFound<K>(K key) => new KeyError(key);
    }
}
