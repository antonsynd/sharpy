using System;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Convert an arbitrary object to its string representation.
        /// Returns <c>"None"</c> for null, Python-style <c>"True"</c>/<c>"False"</c>
        /// for booleans, and <see cref="object.ToString"/> for everything else.
        /// </summary>
        /// <param name="x">The object to convert</param>
        /// <returns>The string representation</returns>
        /// <example>
        /// <code>
        /// str(42)        # "42"
        /// str(3.14)      # "3.14"
        /// str(True)      # "True"
        /// str(None)      # "None"
        /// </code>
        /// </example>
        public static string Str(object x)
        {
            if (x is null)
            {
                return "None";
            }

            if (Optional.TryFormat(x, out var optStr))
            {
                return optStr;
            }

            if (x is bool b)
            {
                return b ? "True" : "False";
            }

            if (x is double d)
            {
                return FormatFloat(d);
            }

            if (x is float f)
            {
                return FormatFloat(f);
            }

            // ValueTuples (System.ValueTuple<...>): format with Python-style
            // parentheses. Default ValueTuple.ToString() omits the trailing
            // comma for single-element tuples ("(1)"); Python requires "(1,)".
            var type = x.GetType();
            if (type.IsValueType && type.FullName != null
                && type.FullName.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
            {
                return FormatValueTupleStr(x, type);
            }

            return x.ToString() ?? "";
        }

        private static string FormatValueTupleStr(object tuple, Type type)
        {
            // ITuple flattens the nested TRest that ValueTuple uses to pack more
            // than 7 elements, so Length/indexer expose the logical arity
            // (e.g. an 8-tuple reports Length 8, not "7 items + a 1-tuple Rest").
            var t = (System.Runtime.CompilerServices.ITuple)tuple;
            var builder = new System.Text.StringBuilder();
            builder.Append('(');
            for (int i = 0; i < t.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(Str(t[i]!));
            }

            // Single-element tuples render with a trailing comma to match Python:
            // str((1,)) == "(1,)". Arity >= 2 is unaffected.
            if (t.Length == 1)
            {
                builder.Append(',');
            }

            builder.Append(')');
            return builder.ToString();
        }

        /// <summary>
        /// Return the string unchanged.
        /// </summary>
        public static string Str(string s)
        {
            return s;
        }

        /// <summary>
        /// Convert a <see cref="char"/> to string without boxing.
        /// </summary>
        public static string Str(char c)
        {
            return c.ToString();
        }

        /// <summary>
        /// Convert an <see cref="int"/> to string without boxing.
        /// </summary>
        public static string Str(int i)
        {
            return i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Convert a <see cref="long"/> to string without boxing.
        /// </summary>
        public static string Str(long l)
        {
            return l.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Convert a <see cref="double"/> to string without boxing.
        /// Formats with Python-compatible trailing <c>.0</c> for whole numbers.
        /// </summary>
        public static string Str(double d)
        {
            return FormatFloat(d);
        }

        /// <summary>
        /// Convert a <see cref="float"/> to string without boxing.
        /// Formats with Python-compatible trailing <c>.0</c> for whole numbers.
        /// </summary>
        public static string Str(float f)
        {
            return FormatFloat(f);
        }

        /// <summary>
        /// Format a floating-point value with Python-compatible representation.
        /// NaN, Infinity, and -Infinity use Python's lowercase forms.
        /// Whole-number values get a trailing <c>.0</c>.
        /// NOTE: Keep in sync with <see cref="FormatFloat(float)"/> overload.
        /// </summary>
        public static string FormatFloat(double value)
        {
            if (double.IsNaN(value))
            {
                return "nan";
            }

            if (double.IsPositiveInfinity(value))
            {
                return "inf";
            }

            if (double.IsNegativeInfinity(value))
            {
                return "-inf";
            }

            var s = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

            // If already contains a decimal point or scientific notation, return as-is
            if (s.IndexOf('.') >= 0 || s.IndexOf('E') >= 0 || s.IndexOf('e') >= 0)
            {
                // Python uses lowercase 'e' in scientific notation (Axiom 2)
                return s.Replace('E', 'e');
            }

            return s + ".0";
        }

        /// <summary>
        /// Format a <see cref="float"/> value with Python-compatible representation.
        /// Overload to avoid float→double widening precision issues.
        /// NOTE: Keep in sync with <see cref="FormatFloat(double)"/> overload.
        /// </summary>
        public static string FormatFloat(float value)
        {
            if (float.IsNaN(value))
            {
                return "nan";
            }

            if (float.IsPositiveInfinity(value))
            {
                return "inf";
            }

            if (float.IsNegativeInfinity(value))
            {
                return "-inf";
            }

            var s = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

            // If already contains a decimal point or scientific notation, return as-is
            if (s.IndexOf('.') >= 0 || s.IndexOf('E') >= 0 || s.IndexOf('e') >= 0)
            {
                // Python uses lowercase 'e' in scientific notation (Axiom 2)
                return s.Replace('E', 'e');
            }

            return s + ".0";
        }

        /// <summary>
        /// Convert a <see cref="bool"/> to string.
        /// Returns Python-style <c>"True"</c> or <c>"False"</c>.
        /// </summary>
        public static string Str(bool b)
        {
            return b ? "True" : "False";
        }
    }
}
