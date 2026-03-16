using System;

namespace Sharpy
{
    /// <summary>
    /// Exception raised for statistics-related errors, similar to Python's
    /// <c>statistics.StatisticsError</c>.
    /// </summary>
    public class StatisticsError : Exception
    {
        /// <summary>Create a StatisticsError with the specified message.</summary>
        public StatisticsError(string message) : base(message) { }
    }
}
