using System;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Convert an arbitrary object to Str.
        /// Returns <c>"None"</c> for null, Python-style <c>"True"</c>/<c>"False"</c>
        /// for booleans, and <see cref="object.ToString"/> for everything else.
        /// </summary>
        /// <param name="x">The object to convert</param>
        /// <returns>The Str representation</returns>
        /// <example>
        /// <code>
        /// str(42)        # "42"
        /// str(3.14)      # "3.14"
        /// str(True)      # "True"
        /// str(None)      # "None"
        /// </code>
        /// </example>
        public static Str Str(object x)
        {
            if (x is null)
            {
                return (Str)"None";
            }

            if (Optional.TryFormat(x, out var optStr))
            {
                return (Str)optStr;
            }

            if (x is bool b)
            {
                return (Str)(b ? "True" : "False");
            }

            if (x is double d)
            {
                return (Str)FormatFloat(d);
            }

            if (x is float f)
            {
                return (Str)FormatFloat(f);
            }

            return (Str)(x.ToString() ?? "");
        }

        /// <summary>
        /// Return the C# <see cref="string"/> as Str.
        /// </summary>
        public static Str Str(string s)
        {
            return (Str)s;
        }

        /// <summary>
        /// Return the Str unchanged.
        /// </summary>
        public static Str Str(Str s)
        {
            return s;
        }

        /// <summary>
        /// Convert a <see cref="char"/> to Str without boxing.
        /// </summary>
        public static Str Str(char c)
        {
            return (Str)c.ToString();
        }

        /// <summary>
        /// Convert an <see cref="int"/> to Str without boxing.
        /// </summary>
        public static Str Str(int i)
        {
            return (Str)i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Convert a <see cref="long"/> to Str without boxing.
        /// </summary>
        public static Str Str(long l)
        {
            return (Str)l.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Convert a <see cref="double"/> to Str without boxing.
        /// Formats with Python-compatible trailing <c>.0</c> for whole numbers.
        /// </summary>
        public static Str Str(double d)
        {
            return (Str)FormatFloat(d);
        }

        /// <summary>
        /// Convert a <see cref="float"/> to Str without boxing.
        /// Formats with Python-compatible trailing <c>.0</c> for whole numbers.
        /// </summary>
        public static Str Str(float f)
        {
            return (Str)FormatFloat(f);
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
        /// Convert a <see cref="bool"/> to Str.
        /// Returns Python-style <c>"True"</c> or <c>"False"</c>.
        /// </summary>
        public static Str Str(bool b)
        {
            return (Str)(b ? "True" : "False");
        }
    }
}
