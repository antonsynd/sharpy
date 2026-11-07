using System;

namespace Sharpy
{
    /// <summary>
    /// Raised when a name is not found. Equivalent to Python's <c>NameError</c>; the base class of
    /// <see cref="UnboundLocalError"/>, as in CPython, so <c>except NameError</c> catches both (#2124).
    /// </summary>
    public class NameError : Exception
    {
        /// <summary>Create a NameError with the specified message.</summary>
        public NameError(string message) : base(message)
        {
        }

        /// <summary>Create a NameError with the specified message and inner exception.</summary>
        public NameError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
