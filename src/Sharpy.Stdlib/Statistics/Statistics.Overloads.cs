using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Statistics
    {
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
