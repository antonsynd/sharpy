using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Statistics
    {
        public static double Mean(IEnumerable<int> data) => Mean(ToDoubles(data));
        public static double Mean(IEnumerable<long> data) => Mean(ToDoubles(data));

        public static double Fmean(IEnumerable<int> data) => Mean(ToDoubles(data));
        public static double Fmean(IEnumerable<long> data) => Mean(ToDoubles(data));

        public static double Median(IEnumerable<int> data) => Median(ToDoubles(data));
        public static double Median(IEnumerable<long> data) => Median(ToDoubles(data));

        public static double MedianLow(IEnumerable<int> data) => MedianLow(ToDoubles(data));
        public static double MedianLow(IEnumerable<long> data) => MedianLow(ToDoubles(data));

        public static double MedianHigh(IEnumerable<int> data) => MedianHigh(ToDoubles(data));
        public static double MedianHigh(IEnumerable<long> data) => MedianHigh(ToDoubles(data));

        public static double Stdev(IEnumerable<int> data) => Stdev(ToDoubles(data));
        public static double Stdev(IEnumerable<long> data) => Stdev(ToDoubles(data));

        public static double Pstdev(IEnumerable<int> data) => Pstdev(ToDoubles(data));
        public static double Pstdev(IEnumerable<long> data) => Pstdev(ToDoubles(data));

        public static double Variance(IEnumerable<int> data) => Variance(ToDoubles(data));
        public static double Variance(IEnumerable<long> data) => Variance(ToDoubles(data));

        public static double Pvariance(IEnumerable<int> data) => Pvariance(ToDoubles(data));
        public static double Pvariance(IEnumerable<long> data) => Pvariance(ToDoubles(data));

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
