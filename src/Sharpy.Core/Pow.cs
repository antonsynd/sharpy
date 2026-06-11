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
        /// <param name="y">The exponent. Must be non-negative; negative exponents
        /// are handled by the caller's floating-point path (e.g. <c>2 ** -1 == 0.5</c>).</param>
        /// <returns>x raised to the power y.</returns>
        /// <exception cref="OverflowError">The result does not fit in an <see cref="int"/>.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">y is negative.</exception>
        public static int CheckedIntPow(int x, int y)
        {
            if (y < 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(y),
                    "CheckedIntPow requires a non-negative exponent; negative exponents use the floating-point power path.");
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
        /// <param name="y">The exponent. Must be non-negative.</param>
        /// <returns>x raised to the power y.</returns>
        /// <exception cref="OverflowError">The result does not fit in a <see cref="long"/>.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">y is negative.</exception>
        public static long CheckedIntPow(long x, long y)
        {
            if (y < 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(y),
                    "CheckedIntPow requires a non-negative exponent; negative exponents use the floating-point power path.");
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
