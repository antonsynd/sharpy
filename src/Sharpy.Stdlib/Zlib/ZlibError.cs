using System;

namespace Sharpy
{
    /// <summary>Represents the base exception for zlib errors.</summary>
    [SharpyModuleType("zlib", "error")]
    public class ZlibError : Exception
    {
        /// <summary>Initializes a new zlib exception with the specified message.</summary>
        public ZlibError(string message) : base(message) { }
    }
}
