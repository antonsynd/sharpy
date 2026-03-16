using System;
using System.IO;

namespace Sharpy
{
    /// <summary>
    /// Raised when an operation or function receives an argument of inappropriate type.
    /// Equivalent to Python's <c>TypeError</c>.
    /// </summary>
    public class TypeError : Exception
    {
        /// <summary>Create a TypeError with the specified message.</summary>
        public TypeError(string message) : base(message)
        {
        }

        /// <summary>Create a TypeError with the specified message and inner exception.</summary>
        public TypeError(string message, Exception innerException) : base(message, innerException)
        {
        }

        internal static TypeError OpNotSupported(string op, string type)
        {
            return new TypeError($"'{op}' not supported for instances of '{type}'");
        }

        internal static TypeError IsNotInterface(string type, string @interface)
        {
            return new TypeError($"'{type}' object is not {@interface}");
        }

        internal static TypeError ArgNone(string method, string arg)
        {
            return new TypeError($"{method}() {arg} argument cannot be None");
        }

        internal static TypeError CanOnlyNot(string verb, string typeA, string notType, string preposition, string typeB)
        {
            return new TypeError($"can only {verb} {typeA} (not \"{notType}\") {preposition} {typeB}");
        }
    }

    /// <summary>
    /// Raised when an operation or function receives an argument with the right type but inappropriate value.
    /// Equivalent to Python's <c>ValueError</c>.
    /// </summary>
    public class ValueError : Exception
    {
        /// <summary>Create a ValueError with the specified message.</summary>
        public ValueError(string message) : base(message)
        {
        }

        /// <summary>Create a ValueError with the specified message and inner exception.</summary>
        public ValueError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when an error is detected that doesn't fall into other categories.
    /// Equivalent to Python's <c>RuntimeError</c>.
    /// </summary>
    public class RuntimeError : Exception
    {
        /// <summary>Create a RuntimeError with the specified message.</summary>
        public RuntimeError(string message) : base(message)
        {
        }

        /// <summary>Create a RuntimeError with the specified message and inner exception.</summary>
        public RuntimeError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when an abstract method requires a derived class to override it.
    /// Equivalent to Python's <c>NotImplementedError</c>.
    /// </summary>
    public class NotImplementedError : Exception
    {
        /// <summary>Create a NotImplementedError with the specified message.</summary>
        public NotImplementedError(string message) : base(message)
        {
        }

        /// <summary>Create a NotImplementedError with the specified message and inner exception.</summary>
        public NotImplementedError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when an attribute reference or assignment fails.
    /// Equivalent to Python's <c>AttributeError</c>.
    /// </summary>
    public class AttributeError : Exception
    {
        /// <summary>Create an AttributeError with the specified message.</summary>
        public AttributeError(string message) : base(message)
        {
        }

        /// <summary>Create an AttributeError with the specified message and inner exception.</summary>
        public AttributeError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when the second argument of a division or modulo operation is zero.
    /// Equivalent to Python's <c>ZeroDivisionError</c>.
    /// </summary>
    public class ZeroDivisionError : Exception
    {
        /// <summary>Create a ZeroDivisionError with the specified message.</summary>
        public ZeroDivisionError(string message) : base(message)
        {
        }

        /// <summary>Create a ZeroDivisionError with the specified message and inner exception.</summary>
        public ZeroDivisionError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when the result of an arithmetic operation is too large to be represented.
    /// Equivalent to Python's <c>OverflowError</c>.
    /// </summary>
    public class OverflowError : Exception
    {
        /// <summary>Create an OverflowError with the specified message.</summary>
        public OverflowError(string message) : base(message)
        {
        }

        /// <summary>Create an OverflowError with the specified message and inner exception.</summary>
        public OverflowError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when an I/O operation fails.
    /// Equivalent to Python's <c>OSError</c> (aliased as <c>IOError</c>).
    /// </summary>
    public class IOError : IOException
    {
        /// <summary>Create an IOError with the specified message.</summary>
        public IOError(string message) : base(message)
        {
        }

        /// <summary>Create an IOError with the specified message and inner exception.</summary>
        public IOError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when a system function returns a system-related error.
    /// Equivalent to Python's <c>OSError</c>. In Python 3, OSError and IOError are aliases.
    /// </summary>
    public class OSError : IOError
    {
        /// <summary>Create an OSError with the specified message.</summary>
        public OSError(string message) : base(message)
        {
        }

        /// <summary>Create an OSError with the specified message and inner exception.</summary>
        public OSError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when a file or directory is requested but not found.
    /// Equivalent to Python's <c>FileNotFoundError</c>.
    /// </summary>
    public class FileNotFoundError : FileNotFoundException
    {
        /// <summary>Create a FileNotFoundError with the specified message.</summary>
        public FileNotFoundError(string message) : base(message)
        {
        }

        /// <summary>Create a FileNotFoundError with the specified message and inner exception.</summary>
        public FileNotFoundError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Lookup error exception (base class for KeyError, IndexError in Python;
    /// used directly for codec/encoding lookup failures).
    /// </summary>
    public class LookupError : Exception
    {
        /// <summary>Create a LookupError with the specified message.</summary>
        public LookupError(string message) : base(message)
        {
        }

        /// <summary>Create a LookupError with the specified message and inner exception.</summary>
        public LookupError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// File exists error — raised when trying to create a file that already exists.
    /// </summary>
    public class FileExistsError : IOException
    {
        /// <summary>Create a FileExistsError with the specified message.</summary>
        public FileExistsError(string message) : base(message)
        {
        }

        /// <summary>Create a FileExistsError with the specified message and inner exception.</summary>
        public FileExistsError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Permission error — raised when an operation lacks sufficient access rights.
    /// </summary>
    public class PermissionError : UnauthorizedAccessException
    {
        /// <summary>Create a PermissionError with the specified message.</summary>
        public PermissionError(string message) : base(message)
        {
        }

        /// <summary>Create a PermissionError with the specified message and inner exception.</summary>
        public PermissionError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Is a directory error — raised when a file operation is attempted on a directory.
    /// </summary>
    public class IsADirectoryError : IOException
    {
        /// <summary>Create an IsADirectoryError with the specified message.</summary>
        public IsADirectoryError(string message) : base(message)
        {
        }

        /// <summary>Create an IsADirectoryError with the specified message and inner exception.</summary>
        public IsADirectoryError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when argument parsing fails.
    /// </summary>
    public class ArgumentError : Exception
    {
        /// <summary>Create an ArgumentError with the specified message.</summary>
        public ArgumentError(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Raised to signal program exit (e.g., after --help).
    /// </summary>
    public class SystemExit : Exception
    {
        /// <summary>The exit code.</summary>
        public int Code { get; }

        /// <summary>Create a SystemExit with the specified exit code.</summary>
        public SystemExit(int code) : base("SystemExit: " + code)
        {
            Code = code;
        }
    }
}
