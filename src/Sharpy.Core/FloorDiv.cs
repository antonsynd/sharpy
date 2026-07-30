namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Returns the floored quotient of <paramref name="x"/> divided by
        /// <paramref name="y"/>, matching CPython's <c>float_floor_div</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>Math.Floor(x / y)</c> is <b>not</b> equivalent: <c>x / y</c> can round up
        /// across an integer boundary, so <c>1.0 // 0.1</c> would give <c>10.0</c> where
        /// CPython gives <c>9.0</c>. Deriving the quotient from the raw <c>fmod</c>
        /// remainder instead keeps the division exact.
        /// </para>
        /// <para>
        /// This is the quotient half of <see cref="Divmod(double, double)"/> — CPython
        /// implements <c>float_floor_div</c> by calling <c>float_divmod</c> and taking the
        /// first element — so the two share this one implementation and the divmod identity
        /// <c>x == (x // y) * y + (x % y)</c> established in #1153 holds for floats.
        /// </para>
        /// </remarks>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>The floored quotient</returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        /// <example>
        /// <code>
        /// FloorDiv(1.0, 0.1)   // 9.0  (not 10.0)
        /// FloorDiv(7.5, 0.1)   // 74.0 (not 75.0)
        /// FloorDiv(-1.0, 0.1)  // -10.0
        /// </code>
        /// </example>
        public static double FloorDiv(double x, double y)
        {
            // Python raises ZeroDivisionError only for an exact zero divisor; a
            // tiny-but-nonzero divisor computes.
            if (y == 0.0)
            {
                throw new ZeroDivisionError("float floor division by zero");
            }

            var raw = x % y;
            var div = (x - raw) / y;
            if (raw != 0.0 && ((raw < 0.0) != (y < 0.0)))
            {
                div -= 1.0;
            }

            if (div != 0.0)
            {
                var quotient = System.Math.Floor(div);
                if (div - quotient > 0.5)
                {
                    quotient += 1.0;
                }

                return quotient;
            }

            // A zero quotient takes the sign of the true quotient rather than whatever sign
            // Math.Floor preserves: CPython gives -0.5 // -1.0 == 0.0 (Math.Floor(-0.0) is
            // -0.0) and -0.0 // 1.0 == -0.0. Math.CopySign is unavailable on netstandard2.1,
            // so the sign bit is read directly — `< 0.0` would not do, being false for -0.0,
            // which is precisely the case that distinguishes these two.
            return double.IsNegative(x / y) ? -0.0 : 0.0;
        }

        /// <summary>
        /// Returns the floored quotient of <paramref name="x"/> divided by
        /// <paramref name="y"/>, matching CPython's <c>float_floor_div</c>.
        /// See the <see cref="FloorDiv(double, double)"/> overload.
        /// </summary>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>The floored quotient</returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        public static float FloorDiv(float x, float y)
        {
            if (y == 0.0f)
            {
                throw new ZeroDivisionError("float floor division by zero");
            }

            var raw = x % y;
            var div = (x - raw) / y;
            if (raw != 0.0f && ((raw < 0.0f) != (y < 0.0f)))
            {
                div -= 1.0f;
            }

            if (div != 0.0f)
            {
                var quotient = (float)System.Math.Floor(div);
                if (div - quotient > 0.5f)
                {
                    quotient += 1.0f;
                }

                return quotient;
            }

            return float.IsNegative(x / y) ? -0.0f : 0.0f;
        }
    }
}
