using System;

namespace Sharpy
{
    /// <summary>
    /// Base class for all requests-related errors.
    /// Equivalent to Python's <c>requests.RequestException</c>.
    /// </summary>
    [SharpyModuleType("requests")]
    public class RequestException : IOError
    {
        /// <summary>The HTTP response associated with this error, if any.</summary>
        public Response? Response { get; }

        /// <summary>Create a RequestException with the specified message.</summary>
        public RequestException(string message) : base(message)
        {
        }

        /// <summary>Create a RequestException with the specified message and inner exception.</summary>
        public RequestException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>Create a RequestException with the specified message and response.</summary>
        public RequestException(string message, Response? response) : base(message)
        {
            Response = response;
        }

        /// <summary>Create a RequestException with the specified message, response, and inner exception.</summary>
        public RequestException(string message, Response? response, Exception innerException) : base(message, innerException)
        {
            Response = response;
        }
    }

    /// <summary>
    /// Raised when a network connectivity failure occurs (DNS failure, refused connection, etc.).
    /// Equivalent to Python's <c>requests.ConnectionError</c>.
    /// </summary>
    [SharpyModuleType("requests")]
    public class ConnectionError : RequestException
    {
        /// <summary>Create a ConnectionError with the specified message.</summary>
        public ConnectionError(string message) : base(message)
        {
        }

        /// <summary>Create a ConnectionError with the specified message and inner exception.</summary>
        public ConnectionError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when a request times out.
    /// Equivalent to Python's <c>requests.Timeout</c>.
    /// </summary>
    /// <remarks>
    /// Note: this name collides with <c>System.Threading.Timeout</c>. Use the fully-qualified
    /// <c>System.Threading.Timeout</c> reference when both are in scope.
    /// </remarks>
    [SharpyModuleType("requests")]
    public class Timeout : RequestException
    {
        /// <summary>Create a Timeout with the specified message.</summary>
        public Timeout(string message) : base(message)
        {
        }

        /// <summary>Create a Timeout with the specified message and inner exception.</summary>
        public Timeout(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when an HTTP error status code is returned and <c>raise_for_status()</c> is called.
    /// Equivalent to Python's <c>requests.HTTPError</c>.
    /// </summary>
    [SharpyModuleType("requests")]
    public class HTTPError : RequestException
    {
        /// <summary>Create an HTTPError with the specified message.</summary>
        public HTTPError(string message) : base(message)
        {
        }

        /// <summary>Create an HTTPError with the specified message and inner exception.</summary>
        public HTTPError(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>Create an HTTPError with the specified message and response.</summary>
        public HTTPError(string message, Response? response) : base(message, response)
        {
        }

        /// <summary>Create an HTTPError with the specified message, response, and inner exception.</summary>
        public HTTPError(string message, Response? response, Exception innerException) : base(message, response, innerException)
        {
        }
    }
}
