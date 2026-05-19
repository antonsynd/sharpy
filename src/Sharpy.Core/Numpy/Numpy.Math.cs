using System;

namespace Sharpy
{
    public static partial class Numpy
    {
        /// <summary>Elementwise square root.</summary>
        public static NdArray<double> Sqrt(NdArray<double> a) => Map(a, System.Math.Sqrt);

        /// <summary>Scalar square root — convenience overload mirroring NumPy.</summary>
        public static double Sqrt(double a) => System.Math.Sqrt(a);

        /// <summary>Elementwise natural exponential.</summary>
        public static NdArray<double> Exp(NdArray<double> a) => Map(a, System.Math.Exp);

        /// <summary>Scalar natural exponential.</summary>
        public static double Exp(double a) => System.Math.Exp(a);

        /// <summary>Elementwise natural logarithm.</summary>
        public static NdArray<double> Log(NdArray<double> a) => Map(a, System.Math.Log);

        /// <summary>Scalar natural logarithm.</summary>
        public static double Log(double a) => System.Math.Log(a);

        /// <summary>Elementwise base-2 logarithm.</summary>
        public static NdArray<double> Log2(NdArray<double> a) => Map(a, x => System.Math.Log(x, 2.0));

        /// <summary>Scalar base-2 logarithm.</summary>
        public static double Log2(double a) => System.Math.Log(a, 2.0);

        /// <summary>Elementwise base-10 logarithm.</summary>
        public static NdArray<double> Log10(NdArray<double> a) => Map(a, System.Math.Log10);

        /// <summary>Scalar base-10 logarithm.</summary>
        public static double Log10(double a) => System.Math.Log10(a);

        /// <summary>Elementwise absolute value.</summary>
        public static NdArray<double> Abs(NdArray<double> a) => Map(a, System.Math.Abs);

        /// <summary>Scalar absolute value.</summary>
        public static double Abs(double a) => System.Math.Abs(a);

        /// <summary>Elementwise sine (radians).</summary>
        public static NdArray<double> Sin(NdArray<double> a) => Map(a, System.Math.Sin);

        /// <summary>Scalar sine.</summary>
        public static double Sin(double a) => System.Math.Sin(a);

        /// <summary>Elementwise cosine (radians).</summary>
        public static NdArray<double> Cos(NdArray<double> a) => Map(a, System.Math.Cos);

        /// <summary>Scalar cosine.</summary>
        public static double Cos(double a) => System.Math.Cos(a);

        /// <summary>Elementwise tangent (radians).</summary>
        public static NdArray<double> Tan(NdArray<double> a) => Map(a, System.Math.Tan);

        /// <summary>Scalar tangent.</summary>
        public static double Tan(double a) => System.Math.Tan(a);

        /// <summary>Elementwise arcsine, returning radians.</summary>
        public static NdArray<double> Arcsin(NdArray<double> a) => Map(a, System.Math.Asin);

        /// <summary>Scalar arcsine.</summary>
        public static double Arcsin(double a) => System.Math.Asin(a);

        /// <summary>Elementwise arccosine, returning radians.</summary>
        public static NdArray<double> Arccos(NdArray<double> a) => Map(a, System.Math.Acos);

        /// <summary>Scalar arccosine.</summary>
        public static double Arccos(double a) => System.Math.Acos(a);

        /// <summary>Elementwise arctangent, returning radians.</summary>
        public static NdArray<double> Arctan(NdArray<double> a) => Map(a, System.Math.Atan);

        /// <summary>Scalar arctangent.</summary>
        public static double Arctan(double a) => System.Math.Atan(a);

        /// <summary>Elementwise floor.</summary>
        public static NdArray<double> Floor(NdArray<double> a) => Map(a, System.Math.Floor);

        /// <summary>Scalar floor.</summary>
        public static double Floor(double a) => System.Math.Floor(a);

        /// <summary>Elementwise ceiling.</summary>
        public static NdArray<double> Ceil(NdArray<double> a) => Map(a, System.Math.Ceiling);

        /// <summary>Scalar ceiling.</summary>
        public static double Ceil(double a) => System.Math.Ceiling(a);

        /// <summary>Elementwise round to <paramref name="decimals"/> decimal places (banker's rounding).</summary>
        public static NdArray<double> Round(NdArray<double> a, int decimals = 0)
        {
            return Map(a, x => System.Math.Round(x, decimals));
        }

        /// <summary>Scalar round to <paramref name="decimals"/> decimal places.</summary>
        public static double Round(double a, int decimals = 0) => System.Math.Round(a, decimals);

        /// <summary>
        /// Apply <paramref name="fn"/> elementwise, producing a fresh C-contiguous result.
        /// </summary>
        private static NdArray<double> Map(NdArray<double> a, Func<double, double> fn)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            var data = new double[a.Size];
            var iter = new BroadcastedIterator<double>(a, a.Shape);
            for (int i = 0; i < a.Size; i++)
            {
                data[i] = fn(iter.Current);
                iter.MoveNext();
            }

            return new NdArray<double>(data, a.Shape);
        }
    }
}
