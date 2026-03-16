using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Mathematical statistics functions, similar to Python's <c>statistics</c> module.
    /// </summary>
    public static partial class Statistics
    {
        /// <summary>
        /// Return the arithmetic mean (average) of <paramref name="data"/>.
        /// </summary>
        /// <param name="data">A sequence of numeric values.</param>
        /// <returns>The arithmetic mean.</returns>
        /// <exception cref="StatisticsError">Thrown if <paramref name="data"/> is empty.</exception>
        /// <example>
        /// <code>
        /// statistics.mean([1, 2, 3, 4, 5])    # 3.0
        /// </code>
        /// </example>
        public static double Mean(IEnumerable<double> data)
        {
            var list = Materialize(data);
            return Sum(list) / list.Count;
        }

        /// <summary>
        /// Return the arithmetic mean of <paramref name="data"/> (integer overload).
        /// </summary>
        public static double Mean(IEnumerable<int> data)
        {
            return Mean(ToDoubles(data));
        }

        /// <summary>
        /// Return the arithmetic mean of <paramref name="data"/> (long overload).
        /// </summary>
        public static double Mean(IEnumerable<long> data)
        {
            return Mean(ToDoubles(data));
        }

        /// <summary>
        /// Return the arithmetic mean of <paramref name="data"/> as a float.
        /// For this implementation, equivalent to <see cref="Mean(IEnumerable{double})"/>.
        /// </summary>
        /// <param name="data">A sequence of numeric values.</param>
        /// <returns>The arithmetic mean.</returns>
        /// <exception cref="StatisticsError">Thrown if <paramref name="data"/> is empty.</exception>
        public static double Fmean(IEnumerable<double> data)
        {
            return Mean(data);
        }

        /// <summary>
        /// Return the arithmetic mean of <paramref name="data"/> as a float (integer overload).
        /// </summary>
        public static double Fmean(IEnumerable<int> data)
        {
            return Mean(ToDoubles(data));
        }

        /// <summary>
        /// Return the arithmetic mean of <paramref name="data"/> as a float (long overload).
        /// </summary>
        public static double Fmean(IEnumerable<long> data)
        {
            return Mean(ToDoubles(data));
        }

        /// <summary>
        /// Return the median (middle value) of <paramref name="data"/>.
        /// When the number of data points is even, the median is the average of the
        /// two middle values.
        /// </summary>
        /// <param name="data">A sequence of numeric values.</param>
        /// <returns>The median value.</returns>
        /// <exception cref="StatisticsError">Thrown if <paramref name="data"/> is empty.</exception>
        /// <example>
        /// <code>
        /// statistics.median([1, 2, 3, 4])    # 2.5
        /// statistics.median([1, 2, 3])       # 2.0
        /// </code>
        /// </example>
        public static double Median(IEnumerable<double> data)
        {
            var sorted = MaterializeSorted(data);
            int n = sorted.Count;
            int mid = n / 2;

            if (n % 2 == 0)
            {
                return (sorted[mid - 1] + sorted[mid]) / 2.0;
            }
            return sorted[mid];
        }

        /// <summary>
        /// Return the median of <paramref name="data"/> (integer overload).
        /// </summary>
        public static double Median(IEnumerable<int> data)
        {
            return Median(ToDoubles(data));
        }

        /// <summary>
        /// Return the median of <paramref name="data"/> (long overload).
        /// </summary>
        public static double Median(IEnumerable<long> data)
        {
            return Median(ToDoubles(data));
        }

        /// <summary>
        /// Return the low median of <paramref name="data"/>.
        /// When the number of data points is even, the low median is the smaller
        /// of the two middle values.
        /// </summary>
        /// <param name="data">A sequence of numeric values.</param>
        /// <returns>The low median value.</returns>
        /// <exception cref="StatisticsError">Thrown if <paramref name="data"/> is empty.</exception>
        public static double MedianLow(IEnumerable<double> data)
        {
            var sorted = MaterializeSorted(data);
            int n = sorted.Count;
            int mid = n / 2;

            if (n % 2 == 0)
            {
                return sorted[mid - 1];
            }
            return sorted[mid];
        }

        /// <summary>
        /// Return the low median of <paramref name="data"/> (integer overload).
        /// </summary>
        public static double MedianLow(IEnumerable<int> data)
        {
            return MedianLow(ToDoubles(data));
        }

        /// <summary>
        /// Return the low median of <paramref name="data"/> (long overload).
        /// </summary>
        public static double MedianLow(IEnumerable<long> data)
        {
            return MedianLow(ToDoubles(data));
        }

        /// <summary>
        /// Return the high median of <paramref name="data"/>.
        /// When the number of data points is even, the high median is the larger
        /// of the two middle values.
        /// </summary>
        /// <param name="data">A sequence of numeric values.</param>
        /// <returns>The high median value.</returns>
        /// <exception cref="StatisticsError">Thrown if <paramref name="data"/> is empty.</exception>
        public static double MedianHigh(IEnumerable<double> data)
        {
            var sorted = MaterializeSorted(data);
            int n = sorted.Count;
            return sorted[n / 2];
        }

        /// <summary>
        /// Return the high median of <paramref name="data"/> (integer overload).
        /// </summary>
        public static double MedianHigh(IEnumerable<int> data)
        {
            return MedianHigh(ToDoubles(data));
        }

        /// <summary>
        /// Return the high median of <paramref name="data"/> (long overload).
        /// </summary>
        public static double MedianHigh(IEnumerable<long> data)
        {
            return MedianHigh(ToDoubles(data));
        }

        /// <summary>
        /// Return the single most common data point from <paramref name="data"/>.
        /// If there are multiple modes (tied), the first encountered value wins.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="data">A sequence of values.</param>
        /// <returns>The most common value.</returns>
        /// <exception cref="StatisticsError">Thrown if <paramref name="data"/> is empty.</exception>
        /// <example>
        /// <code>
        /// statistics.mode([1, 1, 2, 3])    # 1
        /// </code>
        /// </example>
        public static T Mode<T>(IEnumerable<T> data)
        {
            var counts = new Dictionary<T, int>();
            var order = new System.Collections.Generic.List<T>();
            bool any = false;

            foreach (T item in data)
            {
                any = true;
                if (counts.ContainsKey(item))
                {
                    counts[item]++;
                }
                else
                {
                    counts[item] = 1;
                    order.Add(item);
                }
            }

            if (!any)
            {
                throw new StatisticsError("no data");
            }

            T bestValue = order[0];
            int bestCount = counts[bestValue];

            for (int i = 1; i < order.Count; i++)
            {
                T candidate = order[i];
                int candidateCount = counts[candidate];
                if (candidateCount > bestCount)
                {
                    bestValue = candidate;
                    bestCount = candidateCount;
                }
            }

            return bestValue;
        }

        /// <summary>
        /// Return the sample standard deviation (the square root of the sample
        /// variance) of <paramref name="data"/>. Uses <c>n-1</c> in the denominator.
        /// </summary>
        /// <param name="data">A sequence of at least two numeric values.</param>
        /// <returns>The sample standard deviation.</returns>
        /// <exception cref="StatisticsError">Thrown if <paramref name="data"/> has fewer than 2 elements.</exception>
        /// <example>
        /// <code>
        /// statistics.stdev([2, 4, 4, 4, 5, 5, 7, 9])    # ~2.138
        /// </code>
        /// </example>
        public static double Stdev(IEnumerable<double> data)
        {
            return System.Math.Sqrt(Variance(data));
        }

        /// <summary>
        /// Return the sample standard deviation (integer overload).
        /// </summary>
        public static double Stdev(IEnumerable<int> data)
        {
            return Stdev(ToDoubles(data));
        }

        /// <summary>
        /// Return the sample standard deviation (long overload).
        /// </summary>
        public static double Stdev(IEnumerable<long> data)
        {
            return Stdev(ToDoubles(data));
        }

        /// <summary>
        /// Return the population standard deviation (the square root of the
        /// population variance) of <paramref name="data"/>. Uses <c>n</c> in the
        /// denominator.
        /// </summary>
        /// <param name="data">A sequence of numeric values.</param>
        /// <returns>The population standard deviation.</returns>
        /// <exception cref="StatisticsError">Thrown if <paramref name="data"/> is empty.</exception>
        public static double Pstdev(IEnumerable<double> data)
        {
            return System.Math.Sqrt(Pvariance(data));
        }

        /// <summary>
        /// Return the population standard deviation (integer overload).
        /// </summary>
        public static double Pstdev(IEnumerable<int> data)
        {
            return Pstdev(ToDoubles(data));
        }

        /// <summary>
        /// Return the population standard deviation (long overload).
        /// </summary>
        public static double Pstdev(IEnumerable<long> data)
        {
            return Pstdev(ToDoubles(data));
        }

        /// <summary>
        /// Return the sample variance of <paramref name="data"/>. Uses <c>n-1</c>
        /// in the denominator (Bessel's correction).
        /// </summary>
        /// <param name="data">A sequence of at least two numeric values.</param>
        /// <returns>The sample variance.</returns>
        /// <exception cref="StatisticsError">Thrown if <paramref name="data"/> has fewer than 2 elements.</exception>
        public static double Variance(IEnumerable<double> data)
        {
            var list = Materialize(data);
            if (list.Count < 2)
            {
                throw new StatisticsError("variance requires at least two data points");
            }

            double mean = Sum(list) / list.Count;
            double ss = SumOfSquaredDeviations(list, mean);
            return ss / (list.Count - 1);
        }

        /// <summary>
        /// Return the sample variance (integer overload).
        /// </summary>
        public static double Variance(IEnumerable<int> data)
        {
            return Variance(ToDoubles(data));
        }

        /// <summary>
        /// Return the sample variance (long overload).
        /// </summary>
        public static double Variance(IEnumerable<long> data)
        {
            return Variance(ToDoubles(data));
        }

        /// <summary>
        /// Return the population variance of <paramref name="data"/>. Uses <c>n</c>
        /// in the denominator.
        /// </summary>
        /// <param name="data">A sequence of numeric values.</param>
        /// <returns>The population variance.</returns>
        /// <exception cref="StatisticsError">Thrown if <paramref name="data"/> is empty.</exception>
        public static double Pvariance(IEnumerable<double> data)
        {
            var list = Materialize(data);
            double mean = Sum(list) / list.Count;
            double ss = SumOfSquaredDeviations(list, mean);
            return ss / list.Count;
        }

        /// <summary>
        /// Return the population variance (integer overload).
        /// </summary>
        public static double Pvariance(IEnumerable<int> data)
        {
            return Pvariance(ToDoubles(data));
        }

        /// <summary>
        /// Return the population variance (long overload).
        /// </summary>
        public static double Pvariance(IEnumerable<long> data)
        {
            return Pvariance(ToDoubles(data));
        }

        // -- Private helpers --------------------------------------------------

        private static System.Collections.Generic.List<double> Materialize(IEnumerable<double> data)
        {
            // Always copy to avoid mutating the caller's collection.
            var list = new System.Collections.Generic.List<double>(data);

            if (list.Count == 0)
            {
                throw new StatisticsError("no data");
            }

            return list;
        }

        private static System.Collections.Generic.List<double> MaterializeSorted(IEnumerable<double> data)
        {
            var list = Materialize(data);
            list.Sort();
            return list;
        }

        private static double Sum(System.Collections.Generic.List<double> list)
        {
            double total = 0;
            foreach (double v in list)
            {
                total += v;
            }
            return total;
        }

        private static double SumOfSquaredDeviations(System.Collections.Generic.List<double> list, double mean)
        {
            double ss = 0;
            foreach (double v in list)
            {
                double diff = v - mean;
                ss += diff * diff;
            }
            return ss;
        }

        private static IEnumerable<double> ToDoubles(IEnumerable<int> data)
        {
            foreach (int v in data)
            {
                yield return v;
            }
        }

        private static IEnumerable<double> ToDoubles(IEnumerable<long> data)
        {
            foreach (long v in data)
            {
                yield return v;
            }
        }
    }
}
