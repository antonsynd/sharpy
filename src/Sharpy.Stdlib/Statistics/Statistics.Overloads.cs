using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Provides statistical functions matching Python's <c>statistics</c> module.
    /// </summary>
    public static partial class Statistics
    {
        /// <summary>Return the sample arithmetic mean of integer data.</summary>
        public static double Mean(IEnumerable<int> data) => Mean(ToDoubles(data));
        /// <summary>Return the sample arithmetic mean of long integer data.</summary>
        public static double Mean(IEnumerable<long> data) => Mean(ToDoubles(data));

        /// <summary>Convert integer data to floats and compute the arithmetic mean.</summary>
        public static double Fmean(IEnumerable<int> data) => Mean(ToDoubles(data));
        /// <summary>Convert long integer data to floats and compute the arithmetic mean.</summary>
        public static double Fmean(IEnumerable<long> data) => Mean(ToDoubles(data));

        /// <summary>Return the median (middle value) of integer data.</summary>
        public static double Median(IEnumerable<int> data) => Median(ToDoubles(data));
        /// <summary>Return the median (middle value) of long integer data.</summary>
        public static double Median(IEnumerable<long> data) => Median(ToDoubles(data));

        /// <summary>Return the low median of integer data.</summary>
        public static double MedianLow(IEnumerable<int> data) => MedianLow(ToDoubles(data));
        /// <summary>Return the low median of long integer data.</summary>
        public static double MedianLow(IEnumerable<long> data) => MedianLow(ToDoubles(data));

        /// <summary>Return the high median of integer data.</summary>
        public static double MedianHigh(IEnumerable<int> data) => MedianHigh(ToDoubles(data));
        /// <summary>Return the high median of long integer data.</summary>
        public static double MedianHigh(IEnumerable<long> data) => MedianHigh(ToDoubles(data));

        /// <summary>Return the square root of the sample variance for integer data.</summary>
        public static double Stdev(IEnumerable<int> data) => Stdev(ToDoubles(data));
        /// <summary>Return the square root of the sample variance for long integer data.</summary>
        public static double Stdev(IEnumerable<long> data) => Stdev(ToDoubles(data));

        /// <summary>Return the square root of the population variance for integer data.</summary>
        public static double Pstdev(IEnumerable<int> data) => Pstdev(ToDoubles(data));
        /// <summary>Return the square root of the population variance for long integer data.</summary>
        public static double Pstdev(IEnumerable<long> data) => Pstdev(ToDoubles(data));

        /// <summary>Return the sample variance of integer data.</summary>
        public static double Variance(IEnumerable<int> data) => Variance(ToDoubles(data));
        /// <summary>Return the sample variance of long integer data.</summary>
        public static double Variance(IEnumerable<long> data) => Variance(ToDoubles(data));

        /// <summary>Return the population variance of integer data.</summary>
        public static double Pvariance(IEnumerable<int> data) => Pvariance(ToDoubles(data));
        /// <summary>Return the population variance of long integer data.</summary>
        public static double Pvariance(IEnumerable<long> data) => Pvariance(ToDoubles(data));

        /// <summary>Return the most common data point from discrete or nominal data.</summary>
        public static T Mode<T>(IEnumerable<T> data) where T : notnull => Mode(new Sharpy.List<T>(data));

        private static Sharpy.List<double> ToDoubles(IEnumerable<int> data)
        {
            var result = new Sharpy.List<double>();
            foreach (int v in data)
                result.Append((double)v);
            return result;
        }

        private static Sharpy.List<double> ToDoubles(IEnumerable<long> data)
        {
            var result = new Sharpy.List<double>();
            foreach (long v in data)
                result.Append((double)v);
            return result;
        }
    }
}
