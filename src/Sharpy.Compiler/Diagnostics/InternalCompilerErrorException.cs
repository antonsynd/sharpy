using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;

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
/// - Optionally, the nearest AST node/span being processed when the error occurred
///
/// This enables better debugging compared to catching generic exceptions
/// and losing context. The optional node/span context is consumed by the
/// last-chance handler (SPY0909) when writing a minimal-repro crash bundle.
/// </remarks>
public sealed class InternalCompilerErrorException : Exception
{
    /// <summary>
    /// The compiler component where the error occurred (e.g., "TypeChecker", "RoslynEmitter").
    /// </summary>
    public string Component { get; }

    /// <summary>
    /// The AST node being processed when the error occurred, if known. Additive context
    /// used to locate a minimal repro; may be null when no node was in scope.
    /// </summary>
    public Node? Node { get; }

    /// <summary>
    /// The source span nearest the error, if known. Falls back to <see cref="Node"/>'s span
    /// when not supplied explicitly.
    /// </summary>
    public TextSpan? Span { get; }

    /// <summary>
    /// Creates a new internal compiler error exception.
    /// </summary>
    /// <param name="component">The compiler component where the error occurred.</param>
    /// <param name="message">A description of what went wrong.</param>
    /// <param name="innerException">The underlying exception that caused this error.</param>
    /// <param name="node">Optional AST node being processed when the error occurred.</param>
    /// <param name="span">Optional source span nearest the error.</param>
    public InternalCompilerErrorException(string component, string message, Exception innerException,
        Node? node = null, TextSpan? span = null)
        : base(FormatMessage(component, message), innerException)
    {
        Component = component;
        Node = node;
        Span = span ?? node?.Span;
    }

    /// <summary>
    /// Creates a new internal compiler error exception without an inner exception.
    /// </summary>
    /// <param name="component">The compiler component where the error occurred.</param>
    /// <param name="message">A description of what went wrong.</param>
    /// <param name="node">Optional AST node being processed when the error occurred.</param>
    /// <param name="span">Optional source span nearest the error.</param>
    public InternalCompilerErrorException(string component, string message,
        Node? node = null, TextSpan? span = null)
        : base(FormatMessage(component, message))
    {
        Component = component;
        Node = node;
        Span = span ?? node?.Span;
    }

    private static string FormatMessage(string component, string message)
    {
        return $"Internal compiler error in {component}: {message}";
    }
}
