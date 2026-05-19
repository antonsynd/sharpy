using System;

namespace Sharpy
{
    /// <summary>
    /// Instance-method shorthands for reductions on <c>NdArray&lt;double&gt;</c>. These forward
    /// to the corresponding <see cref="Numpy"/> module functions to give a Pythonic
    /// <c>arr.sum()</c>-style call surface alongside <c>numpy.sum(arr)</c>.
    /// </summary>
    /// <remarks>
    /// Implemented as extension methods on <c>NdArray&lt;double&gt;</c> rather than instance
    /// methods on the generic <c>NdArray&lt;T&gt;</c>, because reductions are only meaningful
    /// for numeric element types. The generic class would require runtime type dispatch in
    /// every method body; extension methods give the same call site syntax with zero overhead.
    /// </remarks>
    public static class NdArrayReductionExtensions
    {
        /// <summary>Sum of all elements.</summary>
        public static double Sum(this NdArray<double> a) => Numpy.Sum(a);

        /// <summary>Sum along <paramref name="axis"/>, removing that dimension.</summary>
        public static NdArray<double> Sum(this NdArray<double> a, int axis) => Numpy.Sum(a, axis);

        /// <summary>Minimum element.</summary>
        public static double Min(this NdArray<double> a) => Numpy.Min(a);

        /// <summary>Minimum along <paramref name="axis"/>.</summary>
        public static NdArray<double> Min(this NdArray<double> a, int axis) => Numpy.Min(a, axis);

        /// <summary>Maximum element.</summary>
        public static double Max(this NdArray<double> a) => Numpy.Max(a);

        /// <summary>Maximum along <paramref name="axis"/>.</summary>
        public static NdArray<double> Max(this NdArray<double> a, int axis) => Numpy.Max(a, axis);

        /// <summary>Arithmetic mean of all elements.</summary>
        public static double Mean(this NdArray<double> a) => Numpy.Mean(a);

        /// <summary>Mean along <paramref name="axis"/>.</summary>
        public static NdArray<double> Mean(this NdArray<double> a, int axis) => Numpy.Mean(a, axis);

        /// <summary>Population standard deviation.</summary>
        public static double Std(this NdArray<double> a) => Numpy.Std(a);

        /// <summary>Standard deviation along <paramref name="axis"/>.</summary>
        public static NdArray<double> Std(this NdArray<double> a, int axis) => Numpy.Std(a, axis);

        /// <summary>Population variance.</summary>
        public static double Var(this NdArray<double> a) => Numpy.Var(a);

        /// <summary>Variance along <paramref name="axis"/>.</summary>
        public static NdArray<double> Var(this NdArray<double> a, int axis) => Numpy.Var(a, axis);

        /// <summary>Median of all elements.</summary>
        public static double Median(this NdArray<double> a) => Numpy.Median(a);

        /// <summary>Median along <paramref name="axis"/>.</summary>
        public static NdArray<double> Median(this NdArray<double> a, int axis) => Numpy.Median(a, axis);
    }
}
