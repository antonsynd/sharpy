using System;
using System.Globalization;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Parse a string with an explicit base (2, 8, 10, or 16), stripping Python-style
        /// prefixes (0x, 0b, 0o) when they match the base. Used by all per-width integer
        /// conversion builtins.
        /// </summary>
        internal static long ParseIntWithBase(string s, int @base, string funcName)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new ValueError($"invalid literal for {funcName}() with base {@base}: '{s}'");
            }

            s = s.Trim();

            if (@base != 2 && @base != 8 && @base != 10 && @base != 16)
            {
                throw new ValueError($"{funcName}() base must be 2, 8, 10, or 16, not {@base}");
            }

            var negative = false;
            var offset = 0;

            if (s.Length > 0 && (s[0] == '+' || s[0] == '-'))
            {
                negative = s[0] == '-';
                offset = 1;
            }

            var body = s.Substring(offset);

            if (@base == 16 && body.Length > 2
                && body[0] == '0' && (body[1] == 'x' || body[1] == 'X'))
            {
                body = body.Substring(2);
            }
            else if (@base == 2 && body.Length > 2
                && body[0] == '0' && (body[1] == 'b' || body[1] == 'B'))
            {
                body = body.Substring(2);
            }
            else if (@base == 8 && body.Length > 2
                && body[0] == '0' && (body[1] == 'o' || body[1] == 'O'))
            {
                body = body.Substring(2);
            }

            if (body.Length == 0)
            {
                throw new ValueError($"invalid literal for {funcName}() with base {@base}: '{s}'");
            }

            try
            {
                long value = Convert.ToInt64(body, @base);
                return negative ? -value : value;
            }
            catch (FormatException)
            {
                throw new ValueError($"invalid literal for {funcName}() with base {@base}: '{s}'");
            }
            catch (OverflowException)
            {
                throw new OverflowError($"int too large to convert to {funcName}");
            }
        }
    }
}
