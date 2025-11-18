namespace Sharpy.Core;

/// <summary>
/// Raised when a Unicode-related encoding error occurs.
/// </summary>
public class UnicodeEncodeError(string message) : Exception(message)
{
}
