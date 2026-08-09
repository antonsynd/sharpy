using System.Globalization;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Construct zero, matching CPython's <c>Decimal()</c>.
        /// </summary>
        /// <returns>0</returns>
        public static decimal Decimal()
        {
            return 0m;
        }

        /// <summary>
        /// Convert a decimal to decimal (identity).
        /// </summary>
        /// <param name="m">The decimal value</param>
        /// <returns>The same decimal value</returns>
        public static decimal Decimal(decimal m)
        {
            return m;
        }

        /// <summary>
        /// Convert a bool to decimal. True is 1, False is 0.
        /// </summary>
        /// <param name="b">The bool value</param>
        /// <returns>1 for True, 0 for False</returns>
        public static decimal Decimal(bool b)
        {
            return b ? 1m : 0m;
        }

        /// <summary>
        /// Convert an int to decimal.
        /// </summary>
        /// <param name="i">The int value</param>
        /// <returns>The value as a decimal</returns>
        public static decimal Decimal(int i)
        {
            return i;
        }

        /// <summary>
        /// Convert a long to decimal.
        /// </summary>
        /// <param name="l">The long value</param>
        /// <returns>The value as a decimal</returns>
        public static decimal Decimal(long l)
        {
            return l;
        }

        /// <summary>
        /// Convert a float to decimal.
        /// </summary>
        /// <param name="f">The float value</param>
        /// <returns>The value as a decimal</returns>
        /// <exception cref="OverflowError">Value is out of range for decimal</exception>
        public static decimal Decimal(float f)
        {
            return Decimal((double)f);
        }

        /// <summary>
        /// Convert a double to decimal.
        /// </summary>
        /// <param name="d">The double value</param>
        /// <returns>The value as a decimal</returns>
        /// <exception cref="OverflowError">Value is out of range for decimal</exception>
        public static decimal Decimal(double d)
        {
            try
            {
                return (decimal)d;
            }
            catch (System.OverflowException)
            {
                throw new OverflowError($"Value {d} is out of range for decimal");
            }
        }

        /// <summary>
        /// Parse a string as a decimal.
        /// </summary>
        /// <param name="s">The string to parse</param>
        /// <returns>The parsed decimal</returns>
        /// <exception cref="ValueError">The string is not a valid decimal literal</exception>
        public static decimal Decimal(string s)
        {
            if (s == null)
            {
                throw new ValueError("Decimal() argument must be a string, not None");
            }

            if (!decimal.TryParse(s.Trim(), NumberStyles.Number | NumberStyles.AllowExponent,
                    CultureInfo.InvariantCulture, out var result))
            {
                throw new ValueError($"could not convert string to decimal: '{s}'");
            }

            return result;
        }
    }
}
