namespace Sharpy.Core;

public static partial class Exports
{
    /// <summary>
    /// Return the quotient and remainder of dividing x by y.
    /// Uses Python's floored division semantics where the remainder has the same sign as the divisor.
    /// </summary>
    /// <param name="x">The dividend</param>
    /// <param name="y">The divisor</param>
    /// <returns>A tuple of (quotient, remainder)</returns>
    public static (int, int) DivMod(int x, int y)
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
    public static (long, long) DivMod(long x, long y)
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
    public static (double, double) DivMod(double x, double y)
    {
        const double epsilon = 1e-10;
        if (Math.Abs(y) < epsilon)
        {
            throw new DivideByZeroException("float division or modulo by zero");
        }

        var quotient = Math.Floor(x / y);
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
    public static (float, float) DivMod(float x, float y)
    {
        const float epsilon = 1e-7f;
        if (Math.Abs(y) < epsilon)
        {
            throw new DivideByZeroException("float division or modulo by zero");
        }

        var quotient = (float)Math.Floor(x / y);
        var remainder = x - quotient * y;
        return (quotient, remainder);
    }
}
