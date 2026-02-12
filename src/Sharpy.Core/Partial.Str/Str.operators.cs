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
        /// Implements the __add__ dunder method for string concatenation.
        /// Maps to the + operator in Sharpy code.
        /// </summary>
        /// <param name="other">The string to concatenate.</param>
        /// <returns>A new Str with the concatenated result.</returns>
        public Str __Add__(Str other)
        {
            return new Str(_s + other._s);
        }

        /// <summary>
        /// String concatenation operator.
        /// </summary>
        public static Str operator +(Str left, Str right)
        {
            return left.__Add__(right);
        }

        /// <summary>
        /// Implements the __mul__ dunder method for string replication.
        /// Maps to the * operator in Sharpy code.
        /// </summary>
        /// <param name="count">The number of times to replicate the string.</param>
        /// <returns>A new Str with the replicated result.</returns>
        public Str __Mul__(int count)
        {
            if (count <= 0)
            {
                return new Str("");
            }

            var builder = new StringBuilder(_s.Length * count);
            for (int i = 0; i < count; i++)
            {
                builder.Append(_s);
            }

            return new Str(builder.ToString());
        }

        /// <summary>
        /// String replication operator.
        /// </summary>
        public static Str operator *(Str left, int count)
        {
            return left.__Mul__(count);
        }

        /// <summary>
        /// String replication operator (reversed operands).
        /// </summary>
        public static Str operator *(int count, Str right)
        {
            return right.__Mul__(count);
        }

        /// <summary>
        /// Implements the __lt__ dunder method for lexicographical comparison.
        /// Maps to the &lt; operator in Sharpy code.
        /// </summary>
        public bool __Lt__(Str other)
        {
            return string.Compare(_s, other._s, StringComparison.Ordinal) < 0;
        }

        /// <summary>
        /// Implements the __le__ dunder method for lexicographical comparison.
        /// Maps to the &lt;= operator in Sharpy code.
        /// </summary>
        public bool __Le__(Str other)
        {
            return string.Compare(_s, other._s, StringComparison.Ordinal) <= 0;
        }

        /// <summary>
        /// Implements the __gt__ dunder method for lexicographical comparison.
        /// Maps to the &gt; operator in Sharpy code.
        /// </summary>
        public bool __Gt__(Str other)
        {
            return string.Compare(_s, other._s, StringComparison.Ordinal) > 0;
        }

        /// <summary>
        /// Implements the __ge__ dunder method for lexicographical comparison.
        /// Maps to the &gt;= operator in Sharpy code.
        /// </summary>
        public bool __Ge__(Str other)
        {
            return string.Compare(_s, other._s, StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Less than operator for lexicographical comparison.
        /// </summary>
        public static bool operator <(Str left, Str right)
        {
            return left.__Lt__(right);
        }

        /// <summary>
        /// Less than or equal operator for lexicographical comparison.
        /// </summary>
        public static bool operator <=(Str left, Str right)
        {
            return left.__Le__(right);
        }

        /// <summary>
        /// Greater than operator for lexicographical comparison.
        /// </summary>
        public static bool operator >(Str left, Str right)
        {
            return left.__Gt__(right);
        }

        /// <summary>
        /// Greater than or equal operator for lexicographical comparison.
        /// </summary>
        public static bool operator >=(Str left, Str right)
        {
            return left.__Ge__(right);
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
