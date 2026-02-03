using System.Collections.Immutable;

namespace Sharpy.Compiler.Parser.Ast;

// =============================================================================
// FUTURE EXPRESSION NODES (v0.2.x+)
// These are placeholder definitions that follow the immutable pattern.
// Implementation will be completed when these features are developed.
//
// WARNING: These types are defined for forward compatibility but have NO parser,
// semantic analysis, or code generation support. Do not reference them in
// production code paths. Unrecognized AST nodes will trigger diagnostics
// in TypeChecker and RoslynEmitter (see items 1.1 and 1.2).
// =============================================================================

#region Async/Await (v0.2.x+)

/// <summary>
/// Await expression (await expr).
/// Suspends execution until the awaited task completes.
/// </summary>
/// <remarks>
/// PLACEHOLDER: Parser support not yet implemented.
/// Target version: v0.2.x+
/// </remarks>
public record AwaitExpression : Expression
{
    /// <summary>
    /// The expression being awaited (must return a Task or ValueTask).
    /// </summary>
    public Expression Operand { get; init; } = null!;
}

#endregion

#region Pattern Matching (v0.2.x)

/// <summary>
/// Match expression (match expr { case1 => result1, case2 => result2 }).
/// Returns a value based on pattern matching.
/// </summary>
/// <remarks>
/// PLACEHOLDER: Parser support not yet implemented.
/// Target version: v0.2.x
/// </remarks>
public record MatchExpression : Expression
{
    /// <summary>
    /// The expression being matched against patterns.
    /// </summary>
    public Expression Scrutinee { get; init; } = null!;

    /// <summary>
    /// The match arms (pattern => result pairs).
    /// </summary>
    public ImmutableArray<MatchArm> Arms { get; init; } = ImmutableArray<MatchArm>.Empty;
}

/// <summary>
/// A single arm in a match expression (pattern => result).
/// </summary>
public record MatchArm
{
    /// <summary>
    /// The pattern to match against.
    /// </summary>
    public Pattern Pattern { get; init; } = null!;

    /// <summary>
    /// Optional guard condition (when clause).
    /// </summary>
    public Expression? Guard { get; init; }

    /// <summary>
    /// The result expression if the pattern matches.
    /// </summary>
    public Expression Result { get; init; } = null!;

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}

#endregion
