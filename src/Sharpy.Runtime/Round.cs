namespace Sharpy;

public static partial class Exports
{
    /// <summary>
    /// Round a number to the nearest integer.
    /// </summary>
    /// <param name="x">The number to round</param>
    /// <returns>The rounded value</returns>
    public static int Round(double x)
    {
        return (int)Math.Round(x);
    }

    /// <summary>
    /// Round a number to n decimal places.
    /// </summary>
    /// <param name="x">The number to round</param>
    /// <param name="n">The number of decimal places</param>
    /// <returns>The rounded value</returns>
    public static double Round(double x, int n)
    {
        return Math.Round(x, n);
    }

    /// <summary>
    /// Round a float to the nearest integer.
    /// </summary>
    /// <param name="x">The number to round</param>
    /// <returns>The rounded value</returns>
    public static int Round(float x)
    {
        return (int)Math.Round(x);
    }

    /// <summary>
    /// Round a float to n decimal places.
    /// </summary>
    /// <param name="x">The number to round</param>
    /// <param name="n">The number of decimal places</param>
    /// <returns>The rounded value</returns>
    public static float Round(float x, int n)
    {
        return (float)Math.Round(x, n);
    }

    /// <summary>
    /// Round a decimal to the nearest integer.
    /// </summary>
    /// <param name="x">The number to round</param>
    /// <returns>The rounded value</returns>
    public static int Round(decimal x)
    {
        return (int)Math.Round(x);
    }

    /// <summary>
    /// Round a decimal to n decimal places.
    /// </summary>
    /// <param name="x">The number to round</param>
    /// <param name="n">The number of decimal places</param>
    /// <returns>The rounded value</returns>
    public static decimal Round(decimal x, int n)
    {
        return Math.Round(x, n);
    }
}
