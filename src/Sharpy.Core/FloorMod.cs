namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Returns the remainder of Python's floored division of <paramref name="x"/> by
        /// <paramref name="y"/>. The result takes the sign of the divisor (matching Python's
        /// <c>%</c>), unlike C#'s native <c>%</c> which takes the sign of the dividend.
        /// This keeps the divmod identity <c>x == (x // y) * y + FloorMod(x, y)</c> coherent.
        /// </summary>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>The floored-division remainder (sign of the divisor)</returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        /// <example>
        /// <code>
        /// FloorMod(-7, 3)   // 2  (not -1)
        /// FloorMod(7, -3)   // -2
        /// FloorMod(-7, -3)  // -1
        /// </code>
        /// </example>
        public static int FloorMod(int x, int y)
        {
            if (y == 0)
            {
                throw new ZeroDivisionError("integer modulo by zero");
            }

            // Division by -1 leaves no remainder for any dividend, so the answer is 0 by
            // definition — but `x % -1` traps in .NET at int.MinValue even in an unchecked
            // context (a hardware trap, as for `/`). Answering directly avoids inheriting an
            // exception where CPython returns a perfectly representable 0 (#1302). This is the
            // same value `x % -1` produces for every other dividend, so the general path below
            // loses no case.
            if (y == -1)
            {
                return 0;
            }

            var r = x % y;
            // C# `%` has the sign of the dividend; adjust when the remainder is non-zero and
            // its sign differs from the divisor's so the result matches Python's floored `%`.
            if (r != 0 && ((r < 0) != (y < 0)))
            {
                r += y;
            }

            return r;
        }

        /// <summary>
        /// Returns the remainder of Python's floored division of <paramref name="x"/> by
        /// <paramref name="y"/>. The result takes the sign of the divisor.
        /// </summary>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>The floored-division remainder (sign of the divisor)</returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        public static long FloorMod(long x, long y)
        {
            if (y == 0)
            {
                throw new ZeroDivisionError("integer modulo by zero");
            }

            // See the int overload: long.MinValue % -1 traps identically (#1302).
            if (y == -1)
            {
                return 0;
            }

            var r = x % y;
            if (r != 0 && ((r < 0) != (y < 0)))
            {
                r += y;
            }

            return r;
        }

        /// <summary>
        /// Returns the remainder of Python's floored division of <paramref name="x"/> by
        /// <paramref name="y"/>. The result takes the sign of the divisor.
        /// </summary>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>The floored-division remainder (sign of the divisor)</returns>
        /// <remarks>
        /// A zero remainder carries the divisor's sign, matching CPython's <c>float_mod</c>
        /// (<c>-1.0 % 1.0</c> is <c>0.0</c>, <c>1.0 % -1.0</c> is <c>-0.0</c>). C#'s <c>%</c>
        /// gives zero the dividend's sign instead, which is observable in printed output and
        /// in downstream <c>copysign</c>/<c>atan2</c> use.
        /// </remarks>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        public static double FloorMod(double x, double y)
        {
            // Python raises ZeroDivisionError only for an exact zero divisor (C# `double % 0`
            // silently yields NaN); a tiny-but-nonzero divisor does not raise.
            if (y == 0.0)
            {
                throw new ZeroDivisionError("float modulo");
            }

            var r = x % y;
            if (r != 0.0 && ((r < 0.0) != (y < 0.0)))
            {
                r += y;
            }

            // `r == 0.0` is true for both +0.0 and -0.0, so every zero remainder normalizes
            // to the divisor-signed zero CPython produces. Math.CopySign does not exist on
            // netstandard2.1, and the ternary is equivalent here: y is neither zero (guarded
            // above) nor NaN (a NaN divisor makes r NaN, so this branch is unreachable).
            if (r == 0.0)
            {
                return y < 0.0 ? -0.0 : 0.0;
            }

            return r;
        }

        /// <summary>
        /// Returns the remainder of Python's floored division of <paramref name="x"/> by
        /// <paramref name="y"/>. The result takes the sign of the divisor.
        /// </summary>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>The floored-division remainder (sign of the divisor)</returns>
        /// <remarks>
        /// A zero remainder carries the divisor's sign, matching CPython's <c>float_mod</c>.
        /// See the <see cref="FloorMod(double, double)"/> overload.
        /// </remarks>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        public static float FloorMod(float x, float y)
        {
            if (y == 0.0f)
            {
                throw new ZeroDivisionError("float modulo");
            }

            var r = x % y;
            if (r != 0.0f && ((r < 0.0f) != (y < 0.0f)))
            {
                r += y;
            }

            if (r == 0.0f)
            {
                return y < 0.0f ? -0.0f : 0.0f;
            }

            return r;
        }

        /// <summary>
        /// Returns the floored remainder of two <c>ulong</c> operands.
        /// Both operands are non-negative, so the floored remainder is identical
        /// to the truncating remainder. The overload exists so C# overload
        /// resolution selects it instead of widening to <c>double</c> (#1662).
        /// </summary>
        public static ulong FloorMod(ulong x, ulong y)
        {
            if (y == 0UL)
            {
                throw new ZeroDivisionError("integer modulo by zero");
            }

            return x % y;
        }
    }
}
