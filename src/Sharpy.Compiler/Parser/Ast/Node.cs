using System.Collections.Immutable;
using System.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Base class for all AST nodes.
/// </summary>
/// <remarks>
/// <para>
/// This hierarchy supports structural invariant validation via <see cref="ValidateInvariants"/>.
/// Each node type can override this method to validate its own invariants (e.g., non-null
/// required properties). Validation is DEBUG-only to avoid overhead in Release builds.
/// </para>
/// <para>
/// For recursive tree validation, use <see cref="AstValidator.ValidateTree"/> which
/// traverses the AST via <see cref="GetChildNodes"/> and validates each node.
/// </para>
/// </remarks>
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

    /// <summary>
    /// Trivia (comments) preceding this node. Null when trivia preservation is off.
    /// </summary>
    public IReadOnlyList<Trivia>? LeadingTrivia { get; init; }

    /// <summary>
    /// Trivia (end-of-line comments) following this node. Null when trivia preservation is off.
    /// </summary>
    public IReadOnlyList<Trivia>? TrailingTrivia { get; init; }

    /// <summary>
    /// Validates structural invariants of this node.
    /// Override in derived types to add specific checks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Override to validate invariants like:
    /// - Required properties are non-null
    /// - Collections are non-empty when required
    /// - String properties are non-empty when required
    /// </para>
    /// <para>
    /// Use <see cref="Debug.Assert(bool, string)"/> for invariant checks so failures
    /// are visible during development but don't crash production builds.
    /// </para>
    /// <para>
    /// Callers should use <see cref="AstValidator.ValidateTree"/> which is
    /// conditionally compiled for DEBUG builds only.
    /// </para>
    /// </remarks>
    public virtual void ValidateInvariants()
    {
        // Base validation: line/column should be set after parsing
        // Note: LineStart/ColumnStart of 0 is valid for synthetic nodes

        // Span well-formedness: end must not precede start.
        // Skip synthetic nodes where all positions are 0.
        if (LineStart != 0 || ColumnStart != 0 || LineEnd != 0 || ColumnEnd != 0)
        {
            Debug.Assert(LineEnd >= LineStart,
                $"{GetType().Name}: LineEnd ({LineEnd}) < LineStart ({LineStart})");
            Debug.Assert(LineEnd != LineStart || ColumnEnd >= ColumnStart,
                $"{GetType().Name}: same-line span has ColumnEnd ({ColumnEnd}) < ColumnStart ({ColumnStart})");
        }
    }

    /// <summary>
    /// Returns all child nodes of this node for recursive traversal.
    /// Override in derived types to enumerate children.
    /// </summary>
    /// <remarks>
    /// This method enables recursive AST validation and traversal. Each node type
    /// should enumerate all its child nodes (expressions, statements, etc.) but not
    /// non-node properties (strings, enums, etc.).
    /// </remarks>
    /// <returns>An enumerable of child nodes; empty if this node has no children.</returns>
    public virtual IEnumerable<Node> GetChildNodes()
    {
        return Enumerable.Empty<Node>();
    }
}

/// <summary>
/// Root module node containing top-level statements and definitions
/// </summary>
public record Module : Node
{
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public string? DocString { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Body != null, "Module.Body cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes() => Body;
}
