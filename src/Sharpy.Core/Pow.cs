namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return x raised to the power y.
        /// </summary>
        /// <param name="x">The base</param>
        /// <param name="y">The exponent</param>
        /// <returns>x raised to the power y</returns>
        /// <example>
        /// <code>
        /// pow(2, 3)      # 8.0
        /// pow(4, 0.5)    # 2.0
        /// pow(10, -1)    # 0.1
        /// </code>
        /// </example>
        public static double Pow(double x, double y)
        {
            return System.Math.Pow(x, y);
        }

        /// <summary>
        /// Return x raised to the power y.
        /// </summary>
        /// <param name="x">The base</param>
        /// <param name="y">The exponent</param>
        /// <returns>x raised to the power y</returns>
        public static double Pow(int x, int y)
        {
            return System.Math.Pow(x, y);
        }

        /// <summary>
        /// Return x raised to the power y.
        /// </summary>
        /// <param name="x">The base</param>
        /// <param name="y">The exponent</param>
        /// <returns>x raised to the power y</returns>
        public static double Pow(long x, long y)
        {
            return System.Math.Pow(x, y);
        }

        /// <summary>
        /// Return x raised to the power y.
        /// </summary>
        /// <param name="x">The base</param>
        /// <param name="y">The exponent</param>
        /// <returns>x raised to the power y</returns>
        public static float Pow(float x, float y)
        {
            return (float)System.Math.Pow(x, y);
        }

        /// <summary>
        /// Return x raised to the power y as an exact <see cref="int"/> using
        /// checked exponentiation-by-squaring. Unlike <see cref="Pow(int, int)"/>,
        /// this does not route through floating-point and therefore never silently
        /// loses precision or saturates: an out-of-range result raises
        /// <see cref="OverflowError"/>, matching Python's "diagnose, don't saturate"
        /// semantics for fixed-width integers.
        /// </summary>
        /// <param name="x">The base.</param>
        /// <param name="y">The exponent. A negative exponent is handled here rather than by the
        /// caller (#1228) — see the remarks.</param>
        /// <returns>x raised to the power y.</returns>
        /// <exception cref="OverflowError">The result does not fit in an <see cref="int"/>.</exception>
        /// <remarks>
        /// A negative exponent returns the truncating double-path value, which is the spec's rule
        /// for <c>int ** int</c>: <c>2 ** -1</c> is <c>0</c>, not <c>0.5</c>
        /// (arithmetic_operators.md, "Integer ** negative exponent"). This is a DELIBERATE
        /// divergence from CPython, where <c>2 ** -1</c> is the float <c>0.5</c>.
        ///
        /// <para>
        /// Absorbing the case here rather than throwing is what lets the emitter emit ONE
        /// invocation splicing each operand once (#1228). Previously the emitter had to wrap the
        /// call in a <c>y &lt; 0</c> ternary dispatching between this method and a double path,
        /// which regenerated both operands — and regeneration is not pure, since it can re-push
        /// hoisted statements that then run unconditionally.
        /// </para>
        /// </remarks>
        public static int CheckedIntPow(int x, int y)
        {
            if (y < 0)
            {
                return (int)System.Math.Pow(x, y);
            }

            try
            {
                checked
                {
                    int result = 1;
                    int baseValue = x;
                    int exponent = y;

                    while (exponent > 0)
                    {
                        if ((exponent & 1) == 1)
                        {
                            result *= baseValue;
                        }

                        exponent >>= 1;

                        if (exponent > 0)
                        {
                            baseValue *= baseValue;
                        }
                    }

                    return result;
                }
            }
            catch (System.OverflowException ex)
            {
                throw new OverflowError("integer exponentiation result too large for int", ex);
            }
        }

        /// <summary>
        /// Return x raised to the power y as an exact <see cref="long"/> using
        /// checked exponentiation-by-squaring. See <see cref="CheckedIntPow(int, int)"/>
        /// for semantics; an out-of-range result raises <see cref="OverflowError"/>.
        /// </summary>
        /// <param name="x">The base.</param>
        /// <param name="y">The exponent. A negative exponent returns the truncating double-path
        /// value, as in <see cref="CheckedIntPow(int, int)"/> (#1228).</param>
        /// <returns>x raised to the power y.</returns>
        /// <exception cref="OverflowError">The result does not fit in a <see cref="long"/>.</exception>
        public static long CheckedIntPow(long x, long y)
        {
            if (y < 0)
            {
                return (long)System.Math.Pow(x, y);
            }

            try
            {
                checked
                {
                    long result = 1;
                    long baseValue = x;
                    long exponent = y;

                    while (exponent > 0)
                    {
                        if ((exponent & 1) == 1)
                        {
                            result *= baseValue;
                        }

                        exponent >>= 1;

                        if (exponent > 0)
                        {
                            baseValue *= baseValue;
                        }
                    }

                    return result;
                }
            }
            catch (System.OverflowException ex)
            {
                throw new OverflowError("integer exponentiation result too large for long", ex);
            }
        }
    }
}
