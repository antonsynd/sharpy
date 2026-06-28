using System.Collections.Immutable;
using System.Diagnostics;

namespace Sharpy.Compiler.Parser.Ast;

// =============================================================================
// PATTERN AST NODES
// Pattern matching for match expressions/statements.
//
// Implemented patterns (parser + semantic + codegen): WildcardPattern,
// BindingPattern, LiteralPattern, MemberAccessPattern, TuplePattern,
// OrPattern, RelationalPattern, TypePattern, PropertyPattern, PositionalPattern.
//
// Forward-declared patterns (no pipeline support yet):
// UnionCasePattern, ListPattern, AndPattern, GuardPattern.
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
/// Relational operator for relational patterns.
/// </summary>
public enum RelationalOperator
{
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

/// <summary>
/// Relational pattern (e.g., case > 0, case >= 10) - matches by comparison.
/// </summary>
/// <example>
/// case > 0:            // Greater than zero
/// case >= 10:          // Greater than or equal to 10
/// case &lt; 100:          // Less than 100
/// </example>
public record RelationalPattern : Pattern
{
    /// <summary>
    /// The comparison operator.
    /// </summary>
    public RelationalOperator Operator { get; init; }

    /// <summary>
    /// The value to compare against.
    /// </summary>
    public Expression Value { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Value != null, "RelationalPattern.Value cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Value;
    }
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
/// Star (rest) capture inside a sequence pattern, e.g. the <c>*rest</c> in
/// <c>case [a, *rest]</c> or the <c>*init</c> in <c>case [*init, last]</c>.
/// Held inline in <see cref="ListPattern.Elements"/> so its position is preserved
/// (at most one per list pattern). Emits to a C# slice pattern (<c>..</c>).
/// </summary>
public record StarPattern : Pattern
{
    /// <summary>
    /// The capture sub-pattern: a <see cref="BindingPattern"/> for <c>*rest</c> or a
    /// <see cref="WildcardPattern"/> for <c>*_</c>. Null for a bare <c>*</c>.
    /// </summary>
    public Pattern? Capture { get; init; }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        if (Capture != null)
            yield return Capture;
    }
}

/// <summary>
/// A single field in a property pattern (e.g., x=0 in case Point(x=0, y=1)).
/// </summary>
public record PropertyPatternField : Node
{
    /// <summary>
    /// The property name being matched.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// The pattern to match the property value against.
    /// </summary>
    public Pattern Pattern { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Name), "PropertyPatternField.Name cannot be null or empty");
        Debug.Assert(Pattern != null, "PropertyPatternField.Pattern cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Pattern;
    }
}

/// <summary>
/// Property pattern (e.g., case Point(x=0, y=1)) - matches by named properties.
/// </summary>
/// <example>
/// case Point(x=0):         // Match point with x=0
/// case Rect(w=10, h=20):   // Match rect with specific dimensions
/// </example>
public record PropertyPattern : Pattern
{
    /// <summary>
    /// Optional type being matched (e.g., Point in Point(x=0)).
    /// </summary>
    public TypeAnnotation? Type { get; init; }

    /// <summary>
    /// The named property patterns to match.
    /// </summary>
    public ImmutableArray<PropertyPatternField> Fields { get; init; } = ImmutableArray<PropertyPatternField>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Fields != null, "PropertyPattern.Fields cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes() => Fields;
}

/// <summary>
/// Positional pattern (e.g., case Point(1, 2)) - matches by position.
/// </summary>
/// <example>
/// case Point(0, 0):        // Match origin
/// case Pair(x, _):         // Match first element, discard second
/// </example>
public record PositionalPattern : Pattern
{
    /// <summary>
    /// Optional type being matched (e.g., Point in Point(1, 2)).
    /// </summary>
    public TypeAnnotation? Type { get; init; }

    /// <summary>
    /// Positional sub-patterns to match.
    /// </summary>
    public ImmutableArray<Pattern> Elements { get; init; } = ImmutableArray<Pattern>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Elements != null, "PositionalPattern.Elements cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes() => Elements;
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
