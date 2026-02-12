using System;
namespace Sharpy
{
    using System.Text;

    public readonly partial struct Str : IComparable<Str>
    {
        /// <summary>
        /// Compares the current instance with another Str for sorting.
        /// Implements IComparable&lt;Str&gt; for use with .NET comparison operators.
        /// </summary>
        public int CompareTo(Str other)
        {
            return string.Compare(_s, other._s, StringComparison.Ordinal);
        }

        /// <summary>
        /// String concatenation operator.
        /// </summary>
        public static Str operator +(Str left, Str right)
        {
            return new Str(left._s + right._s);
        }

        /// <summary>
        /// String replication operator.
        /// </summary>
        public static Str operator *(Str left, int count)
        {
            if (count <= 0)
            {
                return new Str("");
            }

            var builder = new StringBuilder(left._s.Length * count);
            for (int i = 0; i < count; i++)
            {
                builder.Append(left._s);
            }

            return new Str(builder.ToString());
        }

        /// <summary>
        /// String replication operator (reversed operands).
        /// </summary>
        public static Str operator *(int count, Str right)
        {
            return right * count;
        }

        /// <summary>
        /// Less than operator for lexicographical comparison.
        /// </summary>
        public static bool operator <(Str left, Str right)
        {
            return string.Compare(left._s, right._s, StringComparison.Ordinal) < 0;
        }

        /// <summary>
        /// Less than or equal operator for lexicographical comparison.
        /// </summary>
        public static bool operator <=(Str left, Str right)
        {
            return string.Compare(left._s, right._s, StringComparison.Ordinal) <= 0;
        }

        /// <summary>
        /// Greater than operator for lexicographical comparison.
        /// </summary>
        public static bool operator >(Str left, Str right)
        {
            return string.Compare(left._s, right._s, StringComparison.Ordinal) > 0;
        }

        /// <summary>
        /// Greater than or equal operator for lexicographical comparison.
        /// </summary>
        public static bool operator >=(Str left, Str right)
        {
            return string.Compare(left._s, right._s, StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Implements the __contains__ dunder method for substring test.
        /// Maps to the 'in' operator in Sharpy code.
        /// </summary>
        /// <param name="substring">The substring to search for.</param>
        /// <returns>True if the substring is found, false otherwise.</returns>
        public bool Contains(Str substring)
        {
            return _s.Contains(substring._s);
        }

    }
}
