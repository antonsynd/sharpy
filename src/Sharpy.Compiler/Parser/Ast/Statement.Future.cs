using System.Collections.Immutable;

namespace Sharpy.Compiler.Parser.Ast;

// =============================================================================
// FUTURE STATEMENT NODES (v0.2.x+)
// These are placeholder definitions that follow the immutable pattern.
// Implementation will be completed when these features are developed.
//
// WARNING: These types are defined for forward compatibility but have NO parser,
// semantic analysis, or code generation support. Do not reference them in
// production code paths. Unrecognized AST nodes will trigger diagnostics
// in TypeChecker and RoslynEmitter (see items 1.1 and 1.2).
// =============================================================================

#region Pattern Matching (v0.2.x)

/// <summary>
/// Match statement (match expr: case1: ..., case2: ...).
/// Executes code based on pattern matching (statement form, unlike MatchExpression).
/// </summary>
/// <remarks>
/// PLACEHOLDER: Parser support not yet implemented.
/// Target version: v0.2.x
/// </remarks>
public record MatchStatement : Statement
{
    /// <summary>
    /// The expression being matched against patterns.
    /// </summary>
    public Expression Scrutinee { get; init; } = null!;

    /// <summary>
    /// The match cases (pattern: body pairs).
    /// </summary>
    public ImmutableArray<MatchCase> Cases { get; init; } = ImmutableArray<MatchCase>.Empty;
}

/// <summary>
/// A single case in a match statement (pattern: body).
/// </summary>
public record MatchCase
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
    /// The body statements to execute if the pattern matches.
    /// </summary>
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}

#endregion

#region Tagged Unions / ADTs (v0.2.x)

/// <summary>
/// Union type definition (tagged union / algebraic data type).
/// </summary>
/// <example>
/// union Result[T, E]:
///     case Ok(value: T)
///     case Err(error: E)
/// </example>
/// <remarks>
/// PLACEHOLDER: Parser support not yet implemented.
/// Target version: v0.2.x
/// </remarks>
public record UnionDef : Statement
{
    /// <summary>
    /// The name of the union type.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Type parameters for generic unions (e.g., T, E in Result[T, E]).
    /// </summary>
    public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;

    /// <summary>
    /// The union cases (variants).
    /// </summary>
    public ImmutableArray<UnionCaseDef> Cases { get; init; } = ImmutableArray<UnionCaseDef>.Empty;

    /// <summary>
    /// Decorators applied to the union.
    /// </summary>
    public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;

    /// <summary>
    /// Documentation string.
    /// </summary>
    public string? DocString { get; init; }
}

/// <summary>
/// A single case (variant) in a union type definition.
/// </summary>
/// <example>
/// case Ok(value: T)       // Case with named field
/// case None               // Case with no fields
/// case Tuple(int, str)    // Case with positional fields
/// </example>
public record UnionCaseDef
{
    /// <summary>
    /// The name of this case (e.g., Ok, Err, None).
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Fields for this case. Empty for singleton cases (e.g., None).
    /// </summary>
    public ImmutableArray<UnionCaseField> Fields { get; init; } = ImmutableArray<UnionCaseField>.Empty;

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}

/// <summary>
/// A field in a union case.
/// </summary>
public record UnionCaseField
{
    /// <summary>
    /// The field name (null for positional fields).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The field type.
    /// </summary>
    public TypeAnnotation Type { get; init; } = null!;

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}

#endregion
