namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        /// <param name="x">The number</param>
        /// <returns>The absolute value</returns>
        /// <example>
        /// <code>
        /// abs(-5)      # 5
        /// abs(3)       # 3
        /// abs(-2.5)    # 2.5
        /// </code>
        /// </example>
        public static int Abs(int x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        public static long Abs(long x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        public static double Abs(double x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        public static float Abs(float x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        public static decimal Abs(decimal x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        public static short Abs(short x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        public static sbyte Abs(sbyte x)
        {
            return System.Math.Abs(x);
        }
    }
}
