namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Represents an error during semantic analysis
/// </summary>
public class SemanticError : Exception
{
    public int? Line { get; }
    public int? Column { get; }

    public SemanticError(string message, int? line = null, int? column = null)
        : base(FormatMessage(message, line, column))
    {
        Line = line;
        Column = column;
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
