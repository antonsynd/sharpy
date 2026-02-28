using System.Collections.Immutable;
using System.Diagnostics;

namespace Sharpy.Compiler.Parser.Ast;

// =============================================================================
// FUTURE AND RECENTLY-IMPLEMENTED EXPRESSION NODES
// AwaitExpression and MatchExpression/MatchArm are fully implemented.
// =============================================================================

#region Async/Await

/// <summary>
/// Await expression (await expr).
/// Suspends execution until the awaited task completes.
/// </summary>
public record AwaitExpression : Expression
{
    /// <summary>
    /// The expression being awaited (must return a Task or ValueTask).
    /// </summary>
    public Expression Operand { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Operand != null, "AwaitExpression.Operand cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Operand;
    }
}

#endregion

#region Pattern Matching

/// <summary>
/// Match expression (match expr { case1 => result1, case2 => result2 }).
/// Returns a value based on pattern matching.
/// </summary>
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

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Scrutinee != null, "MatchExpression.Scrutinee cannot be null");
        Debug.Assert(Arms != null, "MatchExpression.Arms cannot be null");
        Debug.Assert(Arms.Length > 0, "MatchExpression.Arms must have at least one arm");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Scrutinee;
        foreach (var arm in Arms)
        {
            yield return arm.Pattern;
            if (arm.Guard != null)
                yield return arm.Guard;
            yield return arm.Result;
        }
    }
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
