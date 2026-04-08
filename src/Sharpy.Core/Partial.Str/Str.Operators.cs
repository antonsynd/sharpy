using System;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Operator overloads for Str: concatenation, repetition, equality,
    /// comparison, and truthiness.
    /// </summary>
    public readonly partial struct Str
    {
        #region Concatenation

        /// <summary>
        /// Concatenates two Str values.
        /// </summary>
        public static Str operator +(Str left, Str right)
        {
            return new Str(left.Value + right.Value);
        }

        #endregion

        #region Repetition

        /// <summary>
        /// Repeats a string a specified number of times.
        /// Python: <c>"ab" * 3  # "ababab"</c>
        /// </summary>
        public static Str operator *(Str left, int count)
        {
            if (count <= 0 || left.Value.Length == 0)
            {
                return new Str("");
            }

            if (count == 1)
            {
                return left;
            }

            var sb = new StringBuilder(left.Value.Length * count);
            for (int i = 0; i < count; i++)
            {
                sb.Append(left.Value);
            }
            return new Str(sb.ToString());
        }

        /// <summary>
        /// Repeats a string a specified number of times (int on left).
        /// Python: <c>3 * "ab"  # "ababab"</c>
        /// </summary>
        public static Str operator *(int count, Str right)
        {
            return right * count;
        }

        /// <summary>
        /// Repeats a string a specified number of times (long count).
        /// Python: <c>"ab" * n  # when n is long</c>
        /// </summary>
        public static Str operator *(Str left, long count)
        {
            if (count > int.MaxValue || count < int.MinValue)
                throw new OverflowError("repeated string is too long");
            return left * (int)count;
        }

        /// <summary>
        /// Repeats a string a specified number of times (long on left).
        /// Python: <c>n * "ab"  # when n is long</c>
        /// </summary>
        public static Str operator *(long count, Str right)
        {
            if (count > int.MaxValue || count < int.MinValue)
                throw new OverflowError("repeated string is too long");
            return right * (int)count;
        }

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether two Str values are equal (ordinal comparison).
        /// </summary>
        public static bool operator ==(Str left, Str right)
        {
            return string.Equals(left.Value, right.Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether two Str values are not equal.
        /// </summary>
        public static bool operator !=(Str left, Str right)
        {
            return !(left == right);
        }

        #endregion

        #region Comparison

        /// <summary>
        /// Lexicographic less-than comparison (ordinal).
        /// </summary>
        public static bool operator <(Str left, Str right)
        {
            return string.Compare(left.Value, right.Value, StringComparison.Ordinal) < 0;
        }

        /// <summary>
        /// Lexicographic less-than-or-equal comparison (ordinal).
        /// </summary>
        public static bool operator <=(Str left, Str right)
        {
            return string.Compare(left.Value, right.Value, StringComparison.Ordinal) <= 0;
        }

        /// <summary>
        /// Lexicographic greater-than comparison (ordinal).
        /// </summary>
        public static bool operator >(Str left, Str right)
        {
            return string.Compare(left.Value, right.Value, StringComparison.Ordinal) > 0;
        }

        /// <summary>
        /// Lexicographic greater-than-or-equal comparison (ordinal).
        /// </summary>
        public static bool operator >=(Str left, Str right)
        {
            return string.Compare(left.Value, right.Value, StringComparison.Ordinal) >= 0;
        }

        #endregion

        #region Truthiness

        /// <summary>
        /// Returns true if the string is non-empty.
        /// </summary>
        public static bool operator true(Str s)
        {
            return s.Value.Length > 0;
        }

        /// <summary>
        /// Returns true if the string is empty.
        /// </summary>
        public static bool operator false(Str s)
        {
            return s.Value.Length == 0;
        }

        #endregion
    }
}
