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
    }
}
