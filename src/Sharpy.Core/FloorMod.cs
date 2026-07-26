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

            return r;
        }
    }
}
