using System;
using System.Net.Sockets;

namespace Sharpy
{
    /// <summary>
    /// Base exception for socket-related errors.
    /// Corresponds to Python's <c>socket.error</c>.
    /// </summary>
    [SharpyModuleType("socket", "error")]
    public class SharpySocketError : Exception
    {
        /// <summary>The system error number associated with this socket error.</summary>
        public int Errno { get; }

        /// <summary>Create a socket error with the specified message and optional errno.</summary>
        public SharpySocketError(string message, int errno = 0) : base(message)
        {
            Errno = errno;
        }

        /// <summary>Create a socket error wrapping an inner exception.</summary>
        public SharpySocketError(string message, Exception inner, int errno = 0) : base(message, inner)
        {
            Errno = errno;
        }

        /// <summary>
        /// Create a <see cref="SharpySocketError"/> from a .NET <see cref="SocketException"/>.
        /// </summary>
        public static SharpySocketError FromSocketException(SocketException ex)
        {
            return new SharpySocketError(ex.Message, ex, (int)ex.SocketErrorCode);
        }
    }

    /// <summary>
    /// Raised when a socket operation times out.
    /// Corresponds to Python's <c>socket.timeout</c>.
    /// </summary>
    [SharpyModuleType("socket", "timeout")]
    public class SharpySocketTimeout : SharpySocketError
    {
        /// <summary>Create a socket timeout error with the specified message.</summary>
        public SharpySocketTimeout(string message, int errno = 0) : base(message, errno) { }

        /// <summary>Create a socket timeout error wrapping an inner exception.</summary>
        public SharpySocketTimeout(string message, Exception inner, int errno = 0) : base(message, inner, errno) { }
    }

    /// <summary>
    /// Raised for address-related errors (e.g., DNS lookup failures).
    /// Corresponds to Python's <c>socket.gaierror</c>.
    /// </summary>
    [SharpyModuleType("socket", "gaierror")]
    public class SharpySocketGaiError : SharpySocketError
    {
        /// <summary>Create a GAI error with the specified message.</summary>
        public SharpySocketGaiError(string message, int errno = 0) : base(message, errno) { }

        /// <summary>Create a GAI error wrapping an inner exception.</summary>
        public SharpySocketGaiError(string message, Exception inner, int errno = 0) : base(message, inner, errno) { }
    }

    /// <summary>
    /// Raised for legacy address-related errors.
    /// Corresponds to Python's <c>socket.herror</c>.
    /// </summary>
    [SharpyModuleType("socket", "herror")]
    public class SharpySocketHError : SharpySocketError
    {
        /// <summary>Create an herror with the specified message.</summary>
        public SharpySocketHError(string message, int errno = 0) : base(message, errno) { }

        /// <summary>Create an herror wrapping an inner exception.</summary>
        public SharpySocketHError(string message, Exception inner, int errno = 0) : base(message, inner, errno) { }
    }
}
