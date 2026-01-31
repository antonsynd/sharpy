namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Represents an error during semantic analysis.
/// Semantic errors are now collected into DiagnosticBag via Compiler.Diagnostics.
/// This type is retained during migration from exception-based to DiagnosticBag-based error handling.
/// </summary>
[Obsolete("Semantic errors should be collected into DiagnosticBag. This type is retained during migration.")]
public class SemanticError : Exception
{
    public int? Line { get; }
    public int? Column { get; }

    /// <summary>
    /// Optional diagnostic error code (e.g., "SHP0200").
    /// </summary>
    public string? Code { get; }

    public SemanticError(string message, int? line = null, int? column = null, string? code = null)
        : base(FormatMessage(message, line, column))
    {
        Line = line;
        Column = column;
        Code = code;
    }

    private static string FormatMessage(string message, int? line, int? column)
    {
        if (line.HasValue && column.HasValue)
        {
            return $"Semantic error at line {line}, column {column}: {message}";
        }
        if (line.HasValue)
        {
            return $"Semantic error at line {line}: {message}";
        }
        return $"Semantic error: {message}";
    }
}
