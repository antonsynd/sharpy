using System.Collections.Immutable;
using System.Diagnostics;

namespace Sharpy.Compiler.Parser.Ast;

// =============================================================================
// PATTERN AST NODES
// Pattern matching for match expressions/statements.
//
// Implemented patterns (parser + semantic + codegen): WildcardPattern,
// BindingPattern, LiteralPattern, MemberAccessPattern, TuplePattern.
//
// Forward-declared patterns (no pipeline support yet): TypePattern,
// UnionCasePattern, ListPattern, OrPattern, AndPattern, GuardPattern.
// =============================================================================

/// <summary>
/// Base class for all pattern nodes in pattern matching.
/// </summary>
public abstract record Pattern : Node;

#region Basic Patterns

/// <summary>
/// Wildcard pattern (_) - matches anything and discards.
/// </summary>
public record WildcardPattern : Pattern;

/// <summary>
/// Binding pattern (name or name: type) - matches anything and binds to variable.
/// </summary>
public record BindingPattern : Pattern
{
    /// <summary>
    /// The identifier for the variable to bind the matched value to.
    /// </summary>
    public Identifier Name { get; init; } = null!;

    /// <summary>
    /// Optional type constraint for the binding.
    /// </summary>
    public TypeAnnotation? Type { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Name != null, "BindingPattern.Name cannot be null");
        Debug.Assert(!string.IsNullOrEmpty(Name.Name), "BindingPattern.Name.Name cannot be null or empty");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Name;
    }
}

/// <summary>
/// Literal pattern - matches a specific constant value.
/// </summary>
public record LiteralPattern : Pattern
{
    /// <summary>
    /// The literal value to match (IntegerLiteral, StringLiteral, etc.).
    /// </summary>
    public Expression Literal { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Literal != null, "LiteralPattern.Literal cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Literal;
    }
}

/// <summary>
/// Type pattern (is Type) - matches if value is of specified type.
/// </summary>
public record TypePattern : Pattern
{
    /// <summary>
    /// The type to check against.
    /// </summary>
    public TypeAnnotation Type { get; init; } = null!;

    /// <summary>
    /// Optional variable to bind the casted value to.
    /// </summary>
    public Identifier? BindingName { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Type != null, "TypePattern.Type cannot be null");
        if (BindingName != null)
        {
            Debug.Assert(!string.IsNullOrEmpty(BindingName.Name),
                "TypePattern.BindingName.Name cannot be null or empty");
        }
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        if (BindingName != null)
            yield return BindingName;
    }
}

/// <summary>
/// Member access pattern (e.g., Color.RED) - matches a specific member value.
/// Used for matching against class constants, enum members, etc.
/// </summary>
public record MemberAccessPattern : Pattern
{
    /// <summary>
    /// The dotted parts of the member access (e.g., ["Color", "RED"]).
    /// </summary>
    public ImmutableArray<string> Parts { get; init; } = ImmutableArray<string>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Parts.Length >= 2, "MemberAccessPattern.Parts must have at least 2 elements");
    }
}

#endregion

#region Compound Patterns

/// <summary>
/// Union case pattern - matches a specific case of a union type.
/// </summary>
/// <example>
/// case Ok(value)      // Destructuring case
/// case None           // Singleton case
/// </example>
public record UnionCasePattern : Pattern
{
    /// <summary>
    /// The union type (if qualified, e.g., Result.Ok).
    /// </summary>
    public TypeAnnotation? UnionType { get; init; }

    /// <summary>
    /// The case name to match.
    /// </summary>
    public string CaseName { get; init; } = "";

    /// <summary>
    /// Patterns to match against the case fields.
    /// </summary>
    public ImmutableArray<Pattern> FieldPatterns { get; init; } = ImmutableArray<Pattern>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(CaseName), "UnionCasePattern.CaseName cannot be null or empty");
        Debug.Assert(FieldPatterns != null, "UnionCasePattern.FieldPatterns cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes() => FieldPatterns;
}

/// <summary>
/// Tuple pattern - matches tuple structure.
/// </summary>
/// <example>
/// (a, b, _)           // Match 3-tuple, bind first two
/// (x, y)              // Match 2-tuple
/// </example>
public record TuplePattern : Pattern
{
    /// <summary>
    /// Patterns for each element.
    /// </summary>
    public ImmutableArray<Pattern> Elements { get; init; } = ImmutableArray<Pattern>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Elements != null, "TuplePattern.Elements cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes() => Elements;
}

/// <summary>
/// List pattern - matches list structure.
/// </summary>
/// <example>
/// []                  // Empty list
/// [x]                 // Single element
/// [head, ...tail]     // Head and rest pattern
/// [a, b, c]           // Exact match
/// </example>
public record ListPattern : Pattern
{
    /// <summary>
    /// Patterns for list elements.
    /// </summary>
    public ImmutableArray<Pattern> Elements { get; init; } = ImmutableArray<Pattern>.Empty;

    /// <summary>
    /// Optional rest pattern (the "...tail" part).
    /// </summary>
    public Pattern? RestPattern { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Elements != null, "ListPattern.Elements cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        foreach (var element in Elements)
            yield return element;
        if (RestPattern != null)
            yield return RestPattern;
    }
}

/// <summary>
/// Or pattern (pattern1 | pattern2) - matches if either pattern matches.
/// </summary>
public record OrPattern : Pattern
{
    /// <summary>
    /// The alternative patterns (at least 2).
    /// </summary>
    public ImmutableArray<Pattern> Alternatives { get; init; } = ImmutableArray<Pattern>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Alternatives != null, "OrPattern.Alternatives cannot be null");
        Debug.Assert(Alternatives.Length >= 2, "OrPattern.Alternatives must have at least 2 elements");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes() => Alternatives;
}

/// <summary>
/// And pattern (pattern1 and pattern2) - matches if both patterns match.
/// Also known as "as pattern" in some languages.
/// </summary>
public record AndPattern : Pattern
{
    /// <summary>
    /// The left pattern.
    /// </summary>
    public Pattern Left { get; init; } = null!;

    /// <summary>
    /// The right pattern.
    /// </summary>
    public Pattern Right { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Left != null, "AndPattern.Left cannot be null");
        Debug.Assert(Right != null, "AndPattern.Right cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Left;
        yield return Right;
    }
}

/// <summary>
/// Guard pattern (pattern when condition) - adds a condition to a pattern.
/// </summary>
public record GuardPattern : Pattern
{
    /// <summary>
    /// The inner pattern.
    /// </summary>
    public Pattern Inner { get; init; } = null!;

    /// <summary>
    /// The guard condition.
    /// </summary>
    public Expression Guard { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Inner != null, "GuardPattern.Inner cannot be null");
        Debug.Assert(Guard != null, "GuardPattern.Guard cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Inner;
        yield return Guard;
    }
}

#endregion
