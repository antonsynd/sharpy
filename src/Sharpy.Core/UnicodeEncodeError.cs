using System;

namespace Sharpy.Core
{
    /// <summary>
    /// Raised when a Unicode-related encoding error occurs.
    /// </summary>
    public class UnicodeEncodeError : Exception
    {
        public UnicodeEncodeError(string message) : base(message) { }
    }
}
