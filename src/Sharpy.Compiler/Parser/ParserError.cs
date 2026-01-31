namespace Sharpy.Compiler.Parser;

/// <summary>
/// Represents an error that occurred during parsing.
/// Parser now collects errors into DiagnosticBag via Parser.Diagnostics.
/// This exception type is retained as a safety net during migration.
/// </summary>
[Obsolete("Parser errors are now collected into DiagnosticBag. This type is retained as internal safety net.")]
public class ParserError : Exception
{
    public int Line { get; }
    public int Column { get; }

    /// <summary>
    /// Diagnostic error code (e.g., "SHP0100"). Assigned at throw site
    /// so the catch boundary doesn't need fragile message string matching.
    /// </summary>
    public string? Code { get; }

    public ParserError(string message, int line, int column, string? code = null)
        : base($"Parser error at line {line}, column {column}: {message}")
    {
        Line = line;
        Column = column;
        Code = code;
    }
}
