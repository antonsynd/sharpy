using System;

namespace Sharpy
{
    /// <summary>
    /// Base exception for email module errors.
    /// Equivalent to Python's <c>email.errors.MessageError</c>.
    /// </summary>
    [SharpyModuleType("email", "MessageError")]
    public class MessageError : Exception
    {
        public MessageError(string message) : base(message) { }
        public MessageError(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>Raised when an email message cannot be parsed.</summary>
    [SharpyModuleType("email", "MessageParseError")]
    public class MessageParseError : MessageError
    {
        public MessageParseError(string message) : base(message) { }
    }

    /// <summary>Raised when a header cannot be parsed.</summary>
    [SharpyModuleType("email", "HeaderParseError")]
    public class HeaderParseError : MessageError
    {
        public HeaderParseError(string message) : base(message) { }
    }
}
