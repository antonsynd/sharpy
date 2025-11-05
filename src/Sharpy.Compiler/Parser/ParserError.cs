namespace Sharpy.Compiler.Parser;

/// <summary>
/// Represents an error that occurred during parsing
/// </summary>
public class ParserError : Exception
{
    public int Line { get; }
    public int Column { get; }

    public ParserError(string message, int line, int column)
        : base($"Parser error at line {line}, column {column}: {message}")
    {
        Line = line;
        Column = column;
    }
}
