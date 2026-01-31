using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Service for reporting compilation diagnostics (errors, warnings, etc.).
/// Centralizes error reporting with consistent formatting.
/// </summary>
public interface IDiagnosticReporter
{
    /// <summary>
    /// Report an error with optional source location.
    /// </summary>
    void ReportError(string message, int? line = null, int? column = null, string? code = null);

    /// <summary>
    /// Report an error at a specific AST node's location.
    /// </summary>
    void ReportError(string message, Node node, string? code = null);

    /// <summary>
    /// Report a warning with optional source location.
    /// </summary>
    void ReportWarning(string message, int? line = null, int? column = null, string? code = null);

    /// <summary>
    /// Report a warning at a specific AST node's location.
    /// </summary>
    void ReportWarning(string message, Node node, string? code = null);

    /// <summary>
    /// Get all diagnostics reported so far.
    /// </summary>
    DiagnosticBag Diagnostics { get; }

    /// <summary>
    /// Check if any errors have been reported.
    /// </summary>
    bool HasErrors { get; }

    /// <summary>
    /// Current file path being compiled (for error messages).
    /// </summary>
    string? CurrentFilePath { get; set; }
}
