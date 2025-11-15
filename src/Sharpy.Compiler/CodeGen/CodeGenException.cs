using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Exception thrown when code generation fails
/// </summary>
public class CodeGenException : Exception
{
    /// <summary>
    /// Line number where the error occurred (1-based)
    /// </summary>
    public int? Line { get; }

    /// <summary>
    /// Column number where the error occurred (1-based)
    /// </summary>
    public int? Column { get; }

    /// <summary>
    /// The AST node that caused the error
    /// </summary>
    public Node? Node { get; }

    /// <summary>
    /// Create a code generation exception
    /// </summary>
    public CodeGenException(string message) : base(message)
    {
    }

    /// <summary>
    /// Create a code generation exception with source location
    /// </summary>
    public CodeGenException(string message, int line, int column) : base(message)
    {
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Create a code generation exception with AST node (includes location)
    /// </summary>
    public CodeGenException(string message, Node node) : base(message)
    {
        Node = node;
        Line = node.LineStart;
        Column = node.ColumnStart;
    }

    /// <summary>
    /// Create a code generation exception with inner exception
    /// </summary>
    public CodeGenException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Create a code generation exception with source location and inner exception
    /// </summary>
    public CodeGenException(string message, int line, int column, Exception innerException) 
        : base(message, innerException)
    {
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Get formatted error message with source location if available
    /// </summary>
    public override string ToString()
    {
        if (Line.HasValue && Column.HasValue)
        {
            return $"CodeGen Error at {Line}:{Column}: {Message}";
        }
        return $"CodeGen Error: {Message}";
    }
}
