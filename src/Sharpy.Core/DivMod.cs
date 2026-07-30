namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return the quotient and remainder of dividing x by y.
        /// Uses Python's floored division semantics where the remainder has the same sign as the divisor.
        /// </summary>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>A tuple of (quotient, remainder)</returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        /// <example>
        /// <code>
        /// divmod(7, 2)     # (3, 1)
        /// divmod(-7, 2)    # (-4, 1)
        /// divmod(10, 3)    # (3, 1)
        /// </code>
        /// </example>
        public static (int, int) Divmod(int x, int y)
        {
            // Guard before delegating so divmod's message wins over FloorMod's.
            if (y == 0)
            {
                throw new ZeroDivisionError("integer division or modulo by zero");
            }

            var remainder = FloorMod(x, y);

            // C# `/` truncates toward zero while floored division rounds down, so the quotient
            // drops by one exactly when the division is inexact and the operand signs differ.
            // Computed from `x / y` rather than `(x - remainder) / y` because the latter
            // overflows for dividends near int.MinValue.
            var quotient = x / y;
            if (remainder != 0 && ((x < 0) != (y < 0)))
            {
                quotient--;
            }

            return (quotient, remainder);
        }

        /// <summary>
        /// Return the quotient and remainder of dividing x by y.
        /// Uses Python's floored division semantics where the remainder has the same sign as the divisor.
        /// </summary>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>A tuple of (quotient, remainder)</returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        public static (long, long) Divmod(long x, long y)
        {
            // Guard before delegating so divmod's message wins over FloorMod's.
            if (y == 0)
            {
                throw new ZeroDivisionError("integer division or modulo by zero");
            }

            var remainder = FloorMod(x, y);

            var quotient = x / y;
            if (remainder != 0 && ((x < 0) != (y < 0)))
            {
                quotient--;
            }

            return (quotient, remainder);
        }

        /// <summary>
        /// Return the quotient and remainder of dividing x by y.
        /// Uses Python's floored division semantics where the remainder has the same sign as the divisor.
        /// </summary>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>A tuple of (quotient, remainder)</returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        public static (double, double) Divmod(double x, double y)
        {
            // Python raises only for an exact zero divisor -- a tiny-but-nonzero divisor computes.
            // The guard runs before delegating so divmod's message wins over FloorMod's.
            if (y == 0.0)
            {
                throw new ZeroDivisionError("float divmod()");
            }

            var remainder = FloorMod(x, y);

            // CPython implements float_floor_div as "float_divmod, take the first element",
            // so the quotient has exactly one implementation here too.
            var quotient = FloorDiv(x, y);

            return (quotient, remainder);
        }

        /// <summary>
        /// Return the quotient and remainder of dividing x by y.
        /// Uses Python's floored division semantics where the remainder has the same sign as the divisor.
        /// </summary>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>A tuple of (quotient, remainder)</returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        public static (float, float) Divmod(float x, float y)
        {
            if (y == 0.0f)
            {
                throw new ZeroDivisionError("float divmod()");
            }

            var remainder = FloorMod(x, y);

            var quotient = FloorDiv(x, y);

            return (quotient, remainder);
        }
    }
}
