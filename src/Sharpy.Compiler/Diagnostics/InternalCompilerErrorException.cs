namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Exception type for internal compiler errors that preserves debugging context.
/// </summary>
/// <remarks>
/// This exception is thrown when an unexpected error occurs during compilation
/// (not a user error). It preserves:
/// - The component name where the error occurred
/// - The full inner exception chain and stack trace
/// - A descriptive message for diagnostics
///
/// This enables better debugging compared to catching generic exceptions
/// and losing context.
/// </remarks>
public sealed class InternalCompilerErrorException : Exception
{
    /// <summary>
    /// The compiler component where the error occurred (e.g., "TypeChecker", "RoslynEmitter").
    /// </summary>
    public string Component { get; }

    /// <summary>
    /// Creates a new internal compiler error exception.
    /// </summary>
    /// <param name="component">The compiler component where the error occurred.</param>
    /// <param name="message">A description of what went wrong.</param>
    /// <param name="innerException">The underlying exception that caused this error.</param>
    public InternalCompilerErrorException(string component, string message, Exception innerException)
        : base(FormatMessage(component, message), innerException)
    {
        Component = component;
    }

    /// <summary>
    /// Creates a new internal compiler error exception without an inner exception.
    /// </summary>
    /// <param name="component">The compiler component where the error occurred.</param>
    /// <param name="message">A description of what went wrong.</param>
    public InternalCompilerErrorException(string component, string message)
        : base(FormatMessage(component, message))
    {
        Component = component;
    }

    private static string FormatMessage(string component, string message)
    {
        return $"Internal compiler error in {component}: {message}";
    }
}
