using System;

namespace Sharpy
{
    /// <summary>
    /// Raised when a Unicode-related encoding error occurs.
    /// </summary>
    public class UnicodeEncodeError : Exception
    {
        /// <summary>Create a UnicodeEncodeError with the specified message.</summary>
        public UnicodeEncodeError(string message) : base(message) { }
    }
}
