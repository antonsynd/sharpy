namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Base class for all AST nodes
/// </summary>
public abstract record Node
{
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}

/// <summary>
/// Root module node containing top-level statements and definitions
/// </summary>
public record Module : Node
{
    public List<Statement> Body { get; init; } = new();
    public string? DocString { get; init; }
}
