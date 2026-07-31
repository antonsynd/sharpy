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

        /// <summary>
        /// Return the quotient and remainder of dividing x by y, using
        /// <b>truncating</b> division semantics where the remainder has the same sign as the
        /// <b>dividend</b>.
        /// </summary>
        /// <remarks>
        /// <para><b>This overload deliberately differs from every sibling above, which are floored.</b>
        /// It is not an inconsistency to tidy up. CPython's <c>Decimal.__divmod__</c> truncates —
        /// <c>divmod(Decimal(-7), Decimal(3))</c> is <c>(-2, -1)</c>, not the <c>(-3, 2)</c> that
        /// <c>divmod(-7, 3)</c> gives — so matching the int/float siblings here would break parity
        /// rather than restore it. Sharpy's floored <c>//</c>/<c>%</c> resolution (#1153) is scoped to
        /// int/long/float operands; decimal <c>//</c> (#1174) and <c>%</c> (#1189) are already native
        /// and truncating, and this overload agrees with them.</para>
        /// <para><b>Zero divisor raises <see cref="InvalidOperation"/>, not
        /// <see cref="ZeroDivisionError"/></b> — also deliberate, also CPython. In CPython
        /// <c>divmod(Decimal(7), Decimal(0))</c> and <c>Decimal(7) % Decimal(0)</c> both raise
        /// <c>InvalidOperation</c> while <c>Decimal(7) // Decimal(0)</c> raises <c>DivisionByZero</c>
        /// (a <c>ZeroDivisionError</c> subclass). <c>divmod</c> follows the <c>%</c> side even though
        /// its quotient half alone would follow the other. The two must not be unified.</para>
        /// <para>The divmod identity <c>x == q * y + r</c> holds for all four sign combinations.</para>
        /// </remarks>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>A tuple of (quotient, remainder)</returns>
        /// <exception cref="InvalidOperation">Thrown when <paramref name="y"/> is zero</exception>
        /// <example>
        /// <code>
        /// divmod(7m, 3m)     # (2, 1)
        /// divmod(-7m, 3m)    # (-2, -1)   -- int divmod(-7, 3) is (-3, 2)
        /// divmod(7m, -3m)    # (-2, 1)
        /// divmod(-7m, -3m)   # (2, -1)
        /// </code>
        /// </example>
        public static (decimal, decimal) Divmod(decimal x, decimal y)
        {
            if (y == 0m)
            {
                throw new InvalidOperation("decimal divmod by zero");
            }

            // decimal.Remainder is exactly what op_Modulus invokes, so this half is the same value
            // decimal `%` produces; decimal.Truncate(decimal.Divide(..)) is what decimal `//` lowers
            // to. divmod is the pair, not a third policy.
            return (decimal.Truncate(decimal.Divide(x, y)), decimal.Remainder(x, y));
        }
    }
}
