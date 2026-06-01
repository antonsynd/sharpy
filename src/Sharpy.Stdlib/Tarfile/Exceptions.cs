using System;

namespace Sharpy
{
    /// <summary>
    /// Base exception for tarfile errors.
    /// Equivalent to Python's <c>tarfile.TarError</c>.
    /// </summary>
    [SharpyModuleType("tarfile", "TarError")]
    public class TarError : Exception
    {
        public TarError(string message) : base(message) { }
        public TarError(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>Raised when a tar archive cannot be read.</summary>
    [SharpyModuleType("tarfile", "ReadError")]
    public class ReadError : TarError
    {
        public ReadError(string message) : base(message) { }
    }

    /// <summary>Raised for unsupported compression methods.</summary>
    [SharpyModuleType("tarfile", "CompressionError")]
    public class CompressionError : TarError
    {
        public CompressionError(string message) : base(message) { }
    }

    /// <summary>Raised when extraction fails.</summary>
    [SharpyModuleType("tarfile", "ExtractError")]
    public class ExtractError : TarError
    {
        public ExtractError(string message) : base(message) { }
        public ExtractError(string message, Exception innerException) : base(message, innerException) { }
    }
}
