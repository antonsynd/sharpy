using System;

namespace Sharpy
{
    /// <summary>
    /// Base class for http module errors.
    /// Equivalent to Python's <c>http.client.HTTPException</c>.
    /// </summary>
    [SharpyModuleType("http")]
    public class HTTPException : IOError
    {
        /// <summary>Create an HTTPException with the specified message.</summary>
        public HTTPException(string message) : base(message)
        {
        }

        /// <summary>Create an HTTPException with the specified message and inner exception.</summary>
        public HTTPException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when an invalid URL is provided.
    /// Equivalent to Python's <c>http.client.InvalidURL</c>.
    /// </summary>
    [SharpyModuleType("http")]
    public class InvalidURL : HTTPException
    {
        /// <summary>Create an InvalidURL with the specified message.</summary>
        public InvalidURL(string message) : base(message)
        {
        }

        /// <summary>Create an InvalidURL with the specified message and inner exception.</summary>
        public InvalidURL(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when response cannot be parsed.
    /// Equivalent to Python's <c>http.client.RemoteDisconnected</c>.
    /// </summary>
    [SharpyModuleType("http")]
    public class RemoteDisconnected : HTTPException
    {
        /// <summary>Create a RemoteDisconnected with the specified message.</summary>
        public RemoteDisconnected(string message) : base(message)
        {
        }

        /// <summary>Create a RemoteDisconnected with the specified message and inner exception.</summary>
        public RemoteDisconnected(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
