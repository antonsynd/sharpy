namespace Sharpy.Compiler.Logging;

/// <summary>
/// Base class for all structured compiler events.
/// Events capture discrete moments in the compilation pipeline for
/// debugging, trace replay, performance profiling, and future telemetry.
/// </summary>
/// <remarks>
/// Events are immutable records designed for serialization and analysis.
/// All timestamps use UTC to ensure consistent ordering across machines.
/// </remarks>
public abstract record CompilerEvent
{
    /// <summary>
    /// UTC timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Optional file path associated with the event.
    /// </summary>
    public string? FilePath { get; init; }
}

/// <summary>
/// Emitted when a compilation phase begins.
/// </summary>
/// <param name="Phase">Name of the phase (e.g., "Lexical Analysis", "Type Checking")</param>
/// <param name="NodeCount">Number of AST nodes or items to process in this phase, if known (0 otherwise)</param>
public sealed record PhaseStartEvent(string Phase, int NodeCount = 0) : CompilerEvent;

/// <summary>
/// Emitted when a compilation phase completes.
/// </summary>
/// <param name="Phase">Name of the phase that completed</param>
/// <param name="Duration">Time taken to complete the phase</param>
/// <param name="ErrorCount">Number of errors encountered during this phase</param>
public sealed record PhaseEndEvent(string Phase, TimeSpan Duration, int ErrorCount = 0) : CompilerEvent;

/// <summary>
/// Emitted when a diagnostic (error, warning, or info) is recorded.
/// </summary>
/// <param name="Code">Diagnostic code (e.g., "SPY0201")</param>
/// <param name="Message">Human-readable diagnostic message</param>
/// <param name="Severity">Diagnostic severity level</param>
/// <param name="Line">1-based line number where the diagnostic occurred</param>
/// <param name="Column">1-based column number where the diagnostic occurred</param>
public sealed record DiagnosticEvent(
    string Code,
    string Message,
    DiagnosticEventSeverity Severity,
    int Line,
    int Column) : CompilerEvent;

/// <summary>
/// Severity levels for diagnostic events.
/// </summary>
public enum DiagnosticEventSeverity
{
    /// <summary>Informational message</summary>
    Info,
    /// <summary>Warning that doesn't prevent compilation</summary>
    Warning,
    /// <summary>Error that prevents successful compilation</summary>
    Error
}

/// <summary>
/// Emitted when a symbol is resolved during semantic analysis.
/// Useful for debugging symbol resolution issues.
/// </summary>
/// <param name="Name">Name of the symbol being resolved</param>
/// <param name="Kind">Kind of symbol (e.g., "Variable", "Function", "Class")</param>
/// <param name="Type">Resolved type of the symbol, if available</param>
public sealed record SymbolResolvedEvent(string Name, string Kind, string? Type) : CompilerEvent;

/// <summary>
/// Emitted when an import is resolved.
/// </summary>
/// <param name="ModuleName">Name of the module being imported</param>
/// <param name="ResolvedPath">Resolved file path or assembly path, if found</param>
/// <param name="Success">Whether the import was resolved successfully</param>
public sealed record ImportResolvedEvent(string ModuleName, string? ResolvedPath, bool Success) : CompilerEvent;

/// <summary>
/// Emitted when code generation produces output.
/// </summary>
/// <param name="OutputType">Type of output (e.g., "CSharp", "IL")</param>
/// <param name="ByteCount">Size of generated output in bytes</param>
public sealed record CodeGenEvent(string OutputType, int ByteCount) : CompilerEvent;
