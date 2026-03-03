using System;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Convert an arbitrary object to <see cref="string"/>.
        /// Returns <c>"None"</c> for null, Python-style <c>"True"</c>/<c>"False"</c>
        /// for booleans, and <see cref="object.ToString"/> for everything else.
        /// </summary>
        public static string Str(object x)
        {
            if (x is null)
            {
                return "None";
            }

            if (x is IOptional opt)
            {
                return opt.IsNone ? "None" : Str(opt.BoxedValue!);
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

            return x.ToString() ?? "";
        }

        /// <summary>
        /// Return the C# <see cref="string"/> unchanged.
        /// </summary>
        public static string Str(string s)
        {
            return s;
        }

        /// <summary>
        /// Convert a <see cref="char"/> to <see cref="string"/> without boxing.
        /// </summary>
        public static string Str(char c)
        {
            return c.ToString();
        }

        /// <summary>
        /// Convert an <see cref="int"/> to <see cref="string"/> without boxing.
        /// </summary>
        public static string Str(int i)
        {
            return i.ToString();
        }

        /// <summary>
        /// Convert a <see cref="long"/> to <see cref="string"/> without boxing.
        /// </summary>
        public static string Str(long l)
        {
            return l.ToString();
        }

        /// <summary>
        /// Convert a <see cref="double"/> to <see cref="string"/> without boxing.
        /// Formats with Python-compatible trailing <c>.0</c> for whole numbers.
        /// </summary>
        public static string Str(double d)
        {
            return FormatFloat(d);
        }

        /// <summary>
        /// Convert a <see cref="float"/> to <see cref="string"/> without boxing.
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
                return s;
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
                return s;
            }

            return s + ".0";
        }

        /// <summary>
        /// Convert a <see cref="bool"/> to <see cref="string"/>.
        /// Returns Python-style <c>"True"</c> or <c>"False"</c>.
        /// </summary>
        public static string Str(bool b)
        {
            return b ? "True" : "False";
        }
    }
}
