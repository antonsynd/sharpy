using System;

namespace Sharpy
{
    /// <summary>
    /// Raised when a Unicode-related encoding or decoding error occurs. Equivalent to Python's
    /// <c>UnicodeError</c>: a <see cref="ValueError"/> subclass and the base class of
    /// <see cref="UnicodeEncodeError"/>, as in CPython, so <c>except ValueError</c> catches a
    /// <c>UnicodeEncodeError</c> (#2124).
    /// </summary>
    public class UnicodeError : ValueError
    {
        /// <summary>Create a UnicodeError with the specified message.</summary>
        public UnicodeError(string message) : base(message)
        {
        }

        /// <summary>Create a UnicodeError with the specified message and inner exception.</summary>
        public UnicodeError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
