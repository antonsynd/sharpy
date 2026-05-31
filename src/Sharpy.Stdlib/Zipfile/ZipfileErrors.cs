using System;

namespace Sharpy
{
    /// <summary>Raised when a ZIP archive is invalid or unreadable.</summary>
    [SharpyModuleType("zipfile")]
    public class BadZipFile : Exception
    {
        /// <summary>Initializes the exception with an error message.</summary>
        public BadZipFile(string message) : base(message) { }
    }

    /// <summary>Raised when a ZIP archive would require ZIP64 support.</summary>
    [SharpyModuleType("zipfile")]
    public class LargeZipFile : Exception
    {
        /// <summary>Initializes the exception with an error message.</summary>
        public LargeZipFile(string message) : base(message) { }
    }
}
