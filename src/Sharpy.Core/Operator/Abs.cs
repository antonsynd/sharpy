using System;

namespace Sharpy
{
    /// <summary>Operator module — functions corresponding to the intrinsic operators of Python.</summary>
    public static partial class Operator
    {
        /// <summary>Return the absolute value of x (decimal).</summary>
        public static decimal Abs(decimal x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>Return the absolute value of x (double).</summary>
        public static double Abs(double x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>Return the absolute value of x (int).</summary>
        public static int Abs(int x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>Return the absolute value of x (long).</summary>
        public static long Abs(long x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>Return the absolute value of x (short).</summary>
        public static short Abs(short x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>Return the absolute value of x (float).</summary>
        public static float Abs(float x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>Return the absolute value of x (sbyte).</summary>
        public static sbyte Abs(sbyte x)
        {
            return System.Math.Abs(x);
        }
    }
}
