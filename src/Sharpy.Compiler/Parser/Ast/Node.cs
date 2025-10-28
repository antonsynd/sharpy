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
/// Base class for all statement nodes
/// </summary>
public abstract record Statement : Node;

/// <summary>
/// Base class for all expression nodes
/// </summary>
public abstract record Expression : Node;

/// <summary>
/// Root module node
/// </summary>
public record Module : Node
{
    public List<Statement> Body { get; init; } = new();
}
