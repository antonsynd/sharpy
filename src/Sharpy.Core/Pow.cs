namespace Sharpy.Core;

public static partial class Exports
{
    /// <summary>
    /// Return x raised to the power y.
    /// </summary>
    /// <param name="x">The base</param>
    /// <param name="y">The exponent</param>
    /// <returns>x raised to the power y</returns>
    public static double Pow(double x, double y)
    {
        return Math.Pow(x, y);
    }

    /// <summary>
    /// Return x raised to the power y.
    /// </summary>
    /// <param name="x">The base</param>
    /// <param name="y">The exponent</param>
    /// <returns>x raised to the power y</returns>
    public static double Pow(int x, int y)
    {
        return Math.Pow(x, y);
    }

    /// <summary>
    /// Return x raised to the power y.
    /// </summary>
    /// <param name="x">The base</param>
    /// <param name="y">The exponent</param>
    /// <returns>x raised to the power y</returns>
    public static double Pow(long x, long y)
    {
        return Math.Pow(x, y);
    }

    /// <summary>
    /// Return x raised to the power y.
    /// </summary>
    /// <param name="x">The base</param>
    /// <param name="y">The exponent</param>
    /// <returns>x raised to the power y</returns>
    public static float Pow(float x, float y)
    {
        return (float)Math.Pow(x, y);
    }
}
