// Generated from src/Sharpy.Stdlib/spy/statistics.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/statistics.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// Mathematical statistics functions (mean, median, variance, etc.).
    /// </summary>
    public static partial class Statistics
    {
        /// <summary>
        /// Return the sample arithmetic mean of data.
        /// </summary>
        public static double Mean(Sharpy.List<double> data)
        {
            Sharpy.List<double> values = _Materialize(data);
            return _Sum(values) / global::Sharpy.Builtins.Len(values);
        }

        /// <summary>
        /// Convert data to floats and compute the arithmetic mean.
        /// </summary>
        public static double Fmean(Sharpy.List<double> data)
        {
            return Mean(data);
        }

        /// <summary>
        /// Return the median (middle value) of numeric data.
        /// </summary>
        public static double Median(Sharpy.List<double> data)
        {
            Sharpy.List<double> sortedData = _MaterializeSorted(data);
            int n = global::Sharpy.Builtins.Len(sortedData);
            int mid = (2 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)(n) / 2)));
            if (n % 2 == 0)
            {
                return (sortedData[mid - 1] + sortedData[mid]) / 2.0d;
            }

            return sortedData[mid];
        }

        /// <summary>
        /// Return the low median of numeric data.
        /// </summary>
        public static double MedianLow(Sharpy.List<double> data)
        {
            Sharpy.List<double> sortedData = _MaterializeSorted(data);
            int n = global::Sharpy.Builtins.Len(sortedData);
            int mid = (2 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)(n) / 2)));
            if (n % 2 == 0)
            {
                return sortedData[mid - 1];
            }

            return sortedData[mid];
        }

        /// <summary>
        /// Return the high median of numeric data.
        /// </summary>
        public static double MedianHigh(Sharpy.List<double> data)
        {
            Sharpy.List<double> sortedData = _MaterializeSorted(data);
            int n = global::Sharpy.Builtins.Len(sortedData);
            return sortedData[(2 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)(n) / 2)))];
        }

        /// <summary>
        /// Return the most common data point from discrete or nominal data.
        /// </summary>
        public static T Mode<T>(Sharpy.List<T> data)
            where T : notnull
        {
            Sharpy.Dict<T, int> counts = new Sharpy.Dict<T, int>()
            {
            };
            Sharpy.List<T> order = new Sharpy.List<T>()
            {
            };
            bool anyItem = false;
            foreach (var __loopVar_0 in data)
            {
                var item = __loopVar_0;
                anyItem = true;
                if (counts.Contains(item))
                {
                    counts[item] = counts[item] + 1;
                }
                else
                {
                    counts[item] = 1;
                    order.Append(item);
                }
            }

            if (!anyItem)
            {
                throw new global::Sharpy.StatisticsError("no data");
            }

            T bestValue = order[0];
            int bestCount = counts[bestValue];
            int i = 1;
            while (i < global::Sharpy.Builtins.Len(order))
            {
                T candidate = order[i];
                int candidateCount = counts[candidate];
                if (candidateCount > bestCount)
                {
                    bestValue = candidate;
                    bestCount = candidateCount;
                }

                i = i + 1;
            }

            return bestValue;
        }

        /// <summary>
        /// Return the square root of the sample variance.
        /// </summary>
        public static double Stdev(Sharpy.List<double> data)
        {
            return global::System.Math.Sqrt(Variance(data));
        }

        /// <summary>
        /// Return the square root of the population variance.
        /// </summary>
        public static double Pstdev(Sharpy.List<double> data)
        {
            return global::System.Math.Sqrt(Pvariance(data));
        }

        /// <summary>
        /// Return the sample variance of data.
        /// </summary>
        public static double Variance(Sharpy.List<double> data)
        {
            Sharpy.List<double> values = _Materialize(data);
            if (global::Sharpy.Builtins.Len(values) < 2)
            {
                throw new global::Sharpy.StatisticsError("variance requires at least two data points");
            }

            double m = _Sum(values) / global::Sharpy.Builtins.Len(values);
            double ss = _SumOfSquaredDeviations(values, m);
            return ss / (global::Sharpy.Builtins.Len(values) - 1);
        }

        /// <summary>
        /// Return the population variance of data.
        /// </summary>
        public static double Pvariance(Sharpy.List<double> data)
        {
            Sharpy.List<double> values = _Materialize(data);
            double m = _Sum(values) / global::Sharpy.Builtins.Len(values);
            double ss = _SumOfSquaredDeviations(values, m);
            return ss / global::Sharpy.Builtins.Len(values);
        }

        /// <summary>
        /// Copy data to a new list, raising StatisticsError if empty.
        /// </summary>
        internal static Sharpy.List<double> _Materialize(Sharpy.List<double> data)
        {
            Sharpy.List<double> result = new global::Sharpy.List<double>(data);
            if (global::Sharpy.Builtins.Len(result) == 0)
            {
                throw new global::Sharpy.StatisticsError("no data");
            }

            return result;
        }

        /// <summary>
        /// Copy data to a sorted list, raising StatisticsError if empty.
        /// </summary>
        internal static Sharpy.List<double> _MaterializeSorted(Sharpy.List<double> data)
        {
            Sharpy.List<double> result = _Materialize(data);
            result.Sort();
            return result;
        }

        /// <summary>
        /// Return the sum of all values in the list.
        /// </summary>
        internal static double _Sum(Sharpy.List<double> values)
        {
            double total = 0.0d;
            foreach (var __loopVar_1 in values)
            {
                var v = __loopVar_1;
                total = total + v;
            }

            return total;
        }

        /// <summary>
        /// Return the sum of squared deviations from the mean m.
        /// </summary>
        internal static double _SumOfSquaredDeviations(Sharpy.List<double> values, double m)
        {
            double ss = 0.0d;
            foreach (var __loopVar_2 in values)
            {
                var v = __loopVar_2;
                double diff = v - m;
                ss = ss + diff * diff;
            }

            return ss;
        }

        /// <summary>
        /// Convert a list of ints to a list of floats.
        /// </summary>
        internal static Sharpy.List<double> _IntsToFloats(Sharpy.List<int> data)
        {
            Sharpy.List<double> result = new Sharpy.List<double>()
            {
            };
            foreach (var __loopVar_3 in data)
            {
                var v = __loopVar_3;
                result.Append(global::Sharpy.Builtins.Float(v));
            }

            return result;
        }

        /// <summary>
        /// Convert a list of longs to a list of floats.
        /// </summary>
        internal static Sharpy.List<double> _LongsToFloats(Sharpy.List<long> data)
        {
            Sharpy.List<double> result = new Sharpy.List<double>()
            {
            };
            foreach (var __loopVar_4 in data)
            {
                var v = __loopVar_4;
                result.Append(global::Sharpy.Builtins.Float(v));
            }

            return result;
        }

        /// <summary>
        /// Return the sample arithmetic mean of integer data.
        /// </summary>
        public static double Mean(Sharpy.List<int> data)
        {
            return Mean(_IntsToFloats(data));
        }

        /// <summary>
        /// Return the sample arithmetic mean of long integer data.
        /// </summary>
        public static double Mean(Sharpy.List<long> data)
        {
            return Mean(_LongsToFloats(data));
        }

        /// <summary>
        /// Convert integer data to floats and compute the arithmetic mean.
        /// </summary>
        public static double Fmean(Sharpy.List<int> data)
        {
            return Mean(_IntsToFloats(data));
        }

        /// <summary>
        /// Convert long integer data to floats and compute the arithmetic mean.
        /// </summary>
        public static double Fmean(Sharpy.List<long> data)
        {
            return Mean(_LongsToFloats(data));
        }

        /// <summary>
        /// Return the median (middle value) of integer data.
        /// </summary>
        public static double Median(Sharpy.List<int> data)
        {
            return Median(_IntsToFloats(data));
        }

        /// <summary>
        /// Return the median (middle value) of long integer data.
        /// </summary>
        public static double Median(Sharpy.List<long> data)
        {
            return Median(_LongsToFloats(data));
        }

        /// <summary>
        /// Return the low median of integer data.
        /// </summary>
        public static double MedianLow(Sharpy.List<int> data)
        {
            return MedianLow(_IntsToFloats(data));
        }

        /// <summary>
        /// Return the low median of long integer data.
        /// </summary>
        public static double MedianLow(Sharpy.List<long> data)
        {
            return MedianLow(_LongsToFloats(data));
        }

        /// <summary>
        /// Return the high median of integer data.
        /// </summary>
        public static double MedianHigh(Sharpy.List<int> data)
        {
            return MedianHigh(_IntsToFloats(data));
        }

        /// <summary>
        /// Return the high median of long integer data.
        /// </summary>
        public static double MedianHigh(Sharpy.List<long> data)
        {
            return MedianHigh(_LongsToFloats(data));
        }

        /// <summary>
        /// Return the square root of the sample variance for integer data.
        /// </summary>
        public static double Stdev(Sharpy.List<int> data)
        {
            return Stdev(_IntsToFloats(data));
        }

        /// <summary>
        /// Return the square root of the sample variance for long integer data.
        /// </summary>
        public static double Stdev(Sharpy.List<long> data)
        {
            return Stdev(_LongsToFloats(data));
        }

        /// <summary>
        /// Return the sample variance of integer data.
        /// </summary>
        public static double Variance(Sharpy.List<int> data)
        {
            return Variance(_IntsToFloats(data));
        }

        /// <summary>
        /// Return the sample variance of long integer data.
        /// </summary>
        public static double Variance(Sharpy.List<long> data)
        {
            return Variance(_LongsToFloats(data));
        }

        /// <summary>
        /// Return the square root of the population variance for integer data.
        /// </summary>
        public static double Pstdev(Sharpy.List<int> data)
        {
            return Pstdev(_IntsToFloats(data));
        }

        /// <summary>
        /// Return the square root of the population variance for long integer data.
        /// </summary>
        public static double Pstdev(Sharpy.List<long> data)
        {
            return Pstdev(_LongsToFloats(data));
        }

        /// <summary>
        /// Return the population variance of integer data.
        /// </summary>
        public static double Pvariance(Sharpy.List<int> data)
        {
            return Pvariance(_IntsToFloats(data));
        }

        /// <summary>
        /// Return the population variance of long integer data.
        /// </summary>
        public static double Pvariance(Sharpy.List<long> data)
        {
            return Pvariance(_LongsToFloats(data));
        }
    }
}
