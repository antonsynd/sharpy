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
    public static partial class Statistics
    {
        public static double Mean(Sharpy.List<double> data)
        {
            Sharpy.List<double> values = _Materialize(data);
            return _Sum(values) / global::Sharpy.Builtins.Len(values);
        }

        public static double Fmean(Sharpy.List<double> data)
        {
            return Mean(data);
        }

        public static double Median(Sharpy.List<double> data)
        {
            Sharpy.List<double> sortedData = _MaterializeSorted(data);
            int n = global::Sharpy.Builtins.Len(sortedData);
            int mid = (2 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)System.Math.Floor((double)((double)(n) / 2)));
            if (n % 2 == 0)
            {
                return (sortedData[mid - 1] + sortedData[mid]) / 2.0d;
            }

            return sortedData[mid];
        }

        public static double MedianLow(Sharpy.List<double> data)
        {
            Sharpy.List<double> sortedData = _MaterializeSorted(data);
            int n = global::Sharpy.Builtins.Len(sortedData);
            int mid = (2 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)System.Math.Floor((double)((double)(n) / 2)));
            if (n % 2 == 0)
            {
                return sortedData[mid - 1];
            }

            return sortedData[mid];
        }

        public static double MedianHigh(Sharpy.List<double> data)
        {
            Sharpy.List<double> sortedData = _MaterializeSorted(data);
            int n = global::Sharpy.Builtins.Len(sortedData);
            return sortedData[(2 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)System.Math.Floor((double)((double)(n) / 2)))];
        }

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

        public static double Stdev(Sharpy.List<double> data)
        {
            return Math.Sqrt(Variance(data));
        }

        public static double Pstdev(Sharpy.List<double> data)
        {
            return Math.Sqrt(Pvariance(data));
        }

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

        public static double Pvariance(Sharpy.List<double> data)
        {
            Sharpy.List<double> values = _Materialize(data);
            double m = _Sum(values) / global::Sharpy.Builtins.Len(values);
            double ss = _SumOfSquaredDeviations(values, m);
            return ss / global::Sharpy.Builtins.Len(values);
        }

        public static Sharpy.List<double> _Materialize(Sharpy.List<double> data)
        {
            Sharpy.List<double> result = new Sharpy.List<double>(data);
            if (global::Sharpy.Builtins.Len(result) == 0)
            {
                throw new global::Sharpy.StatisticsError("no data");
            }

            return result;
        }

        public static Sharpy.List<double> _MaterializeSorted(Sharpy.List<double> data)
        {
            Sharpy.List<double> result = _Materialize(data);
            result.Sort();
            return result;
        }

        public static double _Sum(Sharpy.List<double> values)
        {
            double total = 0.0d;
            foreach (var __loopVar_1 in values)
            {
                var v = __loopVar_1;
                total = total + v;
            }

            return total;
        }

        public static double _SumOfSquaredDeviations(Sharpy.List<double> values, double m)
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
    }
}
