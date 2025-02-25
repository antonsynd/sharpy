using System;

namespace Sharpy
{
    /// <summary>
    /// Raised when a sequence subscript is out of range. (Slice indices are
    /// silently truncated to fall in the allowed range).
    /// </summary>
    public class IndexError(string message) : Exception(message)
    {
    }
}
