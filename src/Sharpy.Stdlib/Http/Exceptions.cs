using System;

namespace Sharpy
{
    /// <summary>
    /// Base exception for http module errors.
    /// Equivalent to Python's <c>http.client.HTTPException</c>.
    /// </summary>
    [SharpyModuleType("http", "HTTPException")]
    public class HTTPException : Exception
    {
        public HTTPException(string message) : base(message) { }
        public HTTPException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Raised when an invalid URL is provided.
    /// Equivalent to Python's <c>http.client.InvalidURL</c>.
    /// </summary>
    [SharpyModuleType("http", "InvalidURL")]
    public class InvalidURL : HTTPException
    {
        public InvalidURL(string message) : base(message) { }
    }

    /// <summary>
    /// Raised when no request has been sent yet.
    /// Equivalent to Python's <c>http.client.NotConnected</c>.
    /// </summary>
    [SharpyModuleType("http", "NotConnected")]
    public class NotConnected : HTTPException
    {
        public NotConnected(string message) : base(message) { }
    }
}
