using System.Collections.Immutable;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Base class for all AST nodes
/// </summary>
public abstract record Node : ILocatable
{
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }

    /// <summary>
    /// Character offset-based span. May be null if not tracked.
    /// This is optional for backward compatibility - existing code that only
    /// sets Line/Column properties will continue to work.
    /// </summary>
    public TextSpan? Span { get; init; }
}

/// <summary>
/// Root module node containing top-level statements and definitions
/// </summary>
public record Module : Node
{
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public string? DocString { get; init; }
}
