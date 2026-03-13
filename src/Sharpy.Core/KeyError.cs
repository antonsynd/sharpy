using System;

namespace Sharpy
{
    /// <summary>
    /// Raised when a mapping (dictionary) key is not found.
    /// </summary>
    public class KeyError : Exception
    {
        /// <summary>Create a KeyError with the specified message.</summary>
        public KeyError(string message) : base(message) { }
    }
}
