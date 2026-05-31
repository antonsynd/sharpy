namespace Sharpy
{
    /// <summary>Raised when gzip data is invalid or unreadable.</summary>
    [SharpyModuleType("gzip")]
    public class BadGzipFile : OSError
    {
        /// <summary>Initializes the exception with an error message.</summary>
        public BadGzipFile(string message) : base(message) { }
    }
}
