namespace Sharpy.Compiler.Lexer;

/// <summary>
/// Represents an error that occurred during lexical analysis.
/// Lexer now collects errors into DiagnosticBag via Lexer.Diagnostics.
/// This exception type is retained as a safety net during migration.
/// </summary>
[Obsolete("Lexer errors are now collected into DiagnosticBag. This type is retained as internal safety net.")]
public class LexerError : Exception
{
    public int Line { get; }
    public int Column { get; }

    public LexerError(string message, int line, int column)
        : base($"Lexer error at line {line}, column {column}: {message}")
    {
        Line = line;
        Column = column;
    }
}
