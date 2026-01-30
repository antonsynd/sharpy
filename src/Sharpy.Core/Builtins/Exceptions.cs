using System;
namespace Sharpy.Core
{
    /// <summary>
    /// Type error exception
    /// </summary>
    public class TypeError : Exception
    {
        public TypeError(string message) : base(message)
        {
        }

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
    /// Value error exception
    /// </summary>
    public class ValueError : Exception
    {
        public ValueError(string message) : base(message)
        {
        }

        public ValueError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Runtime error exception
    /// </summary>
    public class RuntimeError : Exception
    {
        public RuntimeError(string message) : base(message)
        {
        }

        public RuntimeError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Not implemented error exception
    /// </summary>
    public class NotImplementedError : Exception
    {
        public NotImplementedError(string message) : base(message)
        {
        }

        public NotImplementedError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Attribute error exception
    /// </summary>
    public class AttributeError : Exception
    {
        public AttributeError(string message) : base(message)
        {
        }

        public AttributeError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Zero division error exception
    /// </summary>
    public class ZeroDivisionError : Exception
    {
        public ZeroDivisionError(string message) : base(message)
        {
        }

        public ZeroDivisionError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Overflow error exception
    /// </summary>
    public class OverflowError : Exception
    {
        public OverflowError(string message) : base(message)
        {
        }

        public OverflowError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
