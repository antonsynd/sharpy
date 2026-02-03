using System.Collections.Immutable;

namespace Sharpy.Compiler.Parser.Ast;

// =============================================================================
// PATTERN AST NODES (v0.2.x)
// Pattern matching for match expressions/statements and other future uses.
//
// WARNING: These types are defined for forward compatibility but have NO parser,
// semantic analysis, or code generation support. Do not reference them in
// production code paths. Unrecognized AST nodes will trigger diagnostics
// in TypeChecker and RoslynEmitter (see items 1.1 and 1.2).
// =============================================================================

/// <summary>
/// Base class for all pattern nodes in pattern matching.
/// </summary>
/// <remarks>
/// PLACEHOLDER: Parser support not yet implemented.
/// Target version: v0.2.x
/// </remarks>
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
    /// The variable name to bind the matched value to.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Optional type constraint for the binding.
    /// </summary>
    public TypeAnnotation? Type { get; init; }
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
    public string? BindingName { get; init; }
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
}

#endregion
