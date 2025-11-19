namespace Sharpy.Core;

public static partial class Exports
{
    /// <summary>
    /// Round a number to the nearest integer.
    /// </summary>
    /// <param name="x">The number to round</param>
    /// <returns>The rounded value</returns>
    /// <remarks>
    /// Uses .NET's banker's rounding (round half to even). For example, Round(2.5) returns 2, not 3.
    /// </remarks>
    public static int Round(double x)
    {
        return (int)System.Math.Round(x);
    }

    /// <summary>
    /// Round a number to n decimal places.
    /// </summary>
    /// <param name="x">The number to round</param>
    /// <param name="n">The number of decimal places</param>
    /// <returns>The rounded value</returns>
    /// <remarks>
    /// Uses .NET's banker's rounding (round half to even).
    /// </remarks>
    public static double Round(double x, int n)
    {
        return System.Math.Round(x, n);
    }

    /// <summary>
    /// Round a float to the nearest integer.
    /// </summary>
    /// <param name="x">The number to round</param>
    /// <returns>The rounded value</returns>
    /// <remarks>
    /// Uses .NET's banker's rounding (round half to even).
    /// </remarks>
    public static int Round(float x)
    {
        return (int)System.Math.Round(x);
    }

    /// <summary>
    /// Round a float to n decimal places.
    /// </summary>
    /// <param name="x">The number to round</param>
    /// <param name="n">The number of decimal places</param>
    /// <returns>The rounded value</returns>
    /// <remarks>
    /// Uses .NET's banker's rounding (round half to even).
    /// </remarks>
    public static float Round(float x, int n)
    {
        return (float)System.Math.Round(x, n);
    }

    /// <summary>
    /// Round a decimal to the nearest integer.
    /// </summary>
    /// <param name="x">The number to round</param>
    /// <returns>The rounded value</returns>
    /// <remarks>
    /// Uses .NET's banker's rounding (round half to even).
    /// </remarks>
    public static int Round(decimal x)
    {
        return (int)System.Math.Round(x);
    }

    /// <summary>
    /// Round a decimal to n decimal places.
    /// </summary>
    /// <param name="x">The number to round</param>
    /// <param name="n">The number of decimal places</param>
    /// <returns>The rounded value</returns>
    /// <remarks>
    /// Uses .NET's banker's rounding (round half to even).
    /// </remarks>
    public static decimal Round(decimal x, int n)
    {
        return System.Math.Round(x, n);
    }
}
