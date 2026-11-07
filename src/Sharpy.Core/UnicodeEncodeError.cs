using System;

namespace Sharpy
{
    /// <summary>
    /// Raised when a Unicode-related encoding error occurs. A <see cref="UnicodeError"/> (and so
    /// <see cref="ValueError"/>) subclass, as in CPython (#2124).
    /// </summary>
    public class UnicodeEncodeError : UnicodeError
    {
        /// <summary>Create a UnicodeEncodeError with the specified message.</summary>
        public UnicodeEncodeError(string message) : base(message) { }
    }
}
