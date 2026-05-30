using System;

namespace Sharpy
{
    /// <summary>
    /// Exception raised for errors in struct packing/unpacking operations.
    /// Corresponds to Python's struct.error (inherits from Exception, not ValueError).
    /// </summary>
    [SharpyModuleType("struct")]
    public class StructError : Exception
    {
        public StructError(string message) : base(message) { }
    }
}
