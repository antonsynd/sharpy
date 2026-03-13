using System;

namespace Sharpy
{
    /// <summary>
    /// Raised to signal the end of an iterator, similar to Python's StopIteration.
    /// </summary>
    public class StopIteration : Exception
    {
        /// <summary>Create a StopIteration with the default message.</summary>
        public StopIteration() : base("StopIteration") { }
        /// <summary>Create a StopIteration with the specified message.</summary>
        public StopIteration(string message) : base(message) { }
    }
}
