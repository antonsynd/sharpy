namespace Sharpy.Compiler.Lexer;

/// <summary>
/// Represents an error that occurred during lexical analysis
/// </summary>
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
