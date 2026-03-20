using System;
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
        /// <exception cref="DivideByZeroException">Thrown when <paramref name="y"/> is zero</exception>
        /// <example>
        /// <code>
        /// divmod(7, 2)     # (3, 1)
        /// divmod(-7, 2)    # (-4, 1)
        /// divmod(10, 3)    # (3, 1)
        /// </code>
        /// </example>
        public static (int, int) Divmod(int x, int y)
        {
            if (y == 0)
            {
                throw new DivideByZeroException("integer division or modulo by zero");
            }

            var quotient = x / y;
            var remainder = x % y;

            // Adjust for floored division: if remainder is non-zero and signs differ, adjust quotient and remainder
            if (remainder != 0 && ((x < 0) != (y < 0)))
            {
                quotient--;
                remainder += y;
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
        /// <exception cref="DivideByZeroException">Thrown when <paramref name="y"/> is zero</exception>
        public static (long, long) Divmod(long x, long y)
        {
            if (y == 0)
            {
                throw new DivideByZeroException("integer division or modulo by zero");
            }

            var quotient = x / y;
            var remainder = x % y;

            // Adjust for floored division: if remainder is non-zero and signs differ, adjust quotient and remainder
            if (remainder != 0 && ((x < 0) != (y < 0)))
            {
                quotient--;
                remainder += y;
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
        /// <exception cref="DivideByZeroException">Thrown when <paramref name="y"/> is zero</exception>
        public static (double, double) Divmod(double x, double y)
        {
            const double epsilon = 1e-10;
            if (System.Math.Abs(y) < epsilon)
            {
                throw new DivideByZeroException("float division or modulo by zero");
            }

            var quotient = System.Math.Floor(x / y);
            var remainder = x - quotient * y;
            return (quotient, remainder);
        }

        /// <summary>
        /// Return the quotient and remainder of dividing x by y.
        /// Uses Python's floored division semantics where the remainder has the same sign as the divisor.
        /// </summary>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>A tuple of (quotient, remainder)</returns>
        /// <exception cref="DivideByZeroException">Thrown when <paramref name="y"/> is zero</exception>
        public static (float, float) Divmod(float x, float y)
        {
            const float epsilon = 1e-7f;
            if (System.Math.Abs(y) < epsilon)
            {
                throw new DivideByZeroException("float division or modulo by zero");
            }

            var quotient = (float)System.Math.Floor(x / y);
            var remainder = x - quotient * y;
            return (quotient, remainder);
        }
    }
}
