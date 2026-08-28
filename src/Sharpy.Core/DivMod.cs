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

            // Quotient from FloorDiv rather than a third copy of the algorithm (#1226): the two
            // must agree for the divmod identity to hold, and sharing the implementation is what
            // makes that structural instead of a coincidence. It also means the int.MinValue / -1
            // boundary reports Sharpy's OverflowError here, as it does for `//`, rather than
            // leaking a raw System.OverflowException out of a builtin (#1302).
            //
            // DELIBERATE TRADEOFF: this divides twice (once in each helper) where the inlined
            // version divided once, and division is not cheap. Structural correctness was chosen
            // over that — three hand-kept-in-sync copies of a floored-division algorithm is the
            // parallel-site defect this round hit seven times, and "they agree today" is a weaker
            // property than "they cannot disagree". If you are here to make a hot divmod loop
            // faster, inline FloorDiv/FloorMod or let the JIT CSE them; do NOT restore a private
            // third copy of the quotient computation.
            return (FloorDiv(x, y), FloorMod(x, y));
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

            // See the int overload (#1226, #1302).
            return (FloorDiv(x, y), FloorMod(x, y));
        }

        /// <summary>
        /// Return the quotient and remainder of dividing two <c>ulong</c> operands.
        /// Both operands are non-negative, so floored and truncating division coincide; the
        /// overload exists so <c>divmod(uint64, uint64)</c> resolves instead of being refused
        /// (SPY0354) or widened to <c>double</c> — the same reason
        /// <see cref="FloorDiv(ulong, ulong)"/> and <see cref="FloorMod(ulong, ulong)"/> exist (#1662).
        /// </summary>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>A tuple of (quotient, remainder)</returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        public static (ulong, ulong) Divmod(ulong x, ulong y)
        {
            if (y == 0UL)
            {
                throw new ZeroDivisionError("integer division or modulo by zero");
            }

            return (FloorDiv(x, y), FloorMod(x, y));
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
