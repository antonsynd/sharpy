namespace Sharpy;

public static partial class Exports
{
    /// <summary>
    /// Return the quotient and remainder of dividing x by y.
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
        return (quotient, remainder);
    }

    /// <summary>
    /// Return the quotient and remainder of dividing x by y.
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
        return (quotient, remainder);
    }

    /// <summary>
    /// Return the quotient and remainder of dividing x by y.
    /// </summary>
    /// <param name="x">The dividend</param>
    /// <param name="y">The divisor</param>
    /// <returns>A tuple of (quotient, remainder)</returns>
    public static (double, double) DivMod(double x, double y)
    {
        if (y == 0)
        {
            throw new DivideByZeroException("float division or modulo by zero");
        }

        var quotient = Math.Floor(x / y);
        var remainder = x - quotient * y;
        return (quotient, remainder);
    }

    /// <summary>
    /// Return the quotient and remainder of dividing x by y.
    /// </summary>
    /// <param name="x">The dividend</param>
    /// <param name="y">The divisor</param>
    /// <returns>A tuple of (quotient, remainder)</returns>
    public static (float, float) DivMod(float x, float y)
    {
        if (y == 0)
        {
            throw new DivideByZeroException("float division or modulo by zero");
        }

        var quotient = (float)Math.Floor(x / y);
        var remainder = x - quotient * y;
        return (quotient, remainder);
    }
}
