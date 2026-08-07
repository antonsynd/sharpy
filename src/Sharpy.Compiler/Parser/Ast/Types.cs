using System.Collections.Immutable;

namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Type annotation (int, list[str], dict[str, int], Optional[T], etc.)
/// </summary>
/// <remarks>
/// <para>
/// Derives from <see cref="Node"/> so that annotation-shaped operands can key the same
/// node-keyed <c>SemanticInfo</c> channels expression operands use — in particular the
/// type-test lowering the checker records for <c>is</c>/<c>as?</c>/<c>as!</c>, match class
/// patterns and <c>except</c> clauses (#1235). Before this, the record was base-less and
/// re-declared <see cref="Node"/>'s location members, so no dictionary keyed by
/// <see cref="Node"/> could admit an annotation.
/// </para>
/// <para>
/// <b>Deliberately not reachable from <see cref="Node.GetChildNodes"/>.</b> No owning node
/// enumerates its annotations as children, so generic <c>Node</c> traversal still never sees a
/// <see cref="TypeAnnotation"/>. Exposing them would newly surface this type to several
/// hand-rolled <c>switch (node)</c> sites (the lowering-IR rewriter and optimization passes) and
/// to <c>Node</c>-taking walkers such as the await scanners, none of which has an exhaustiveness
/// guard — they would silently take their default arms. Making annotations traversable is a
/// separate change with its own test burden.
/// </para>
/// <para>
/// The sibling annotation records in this file (<see cref="FunctionType"/>,
/// <see cref="TupleType"/>) stay base-less; the asymmetry is intentional, as nothing keys a
/// node-keyed channel by them.
/// </para>
/// </remarks>
public record TypeAnnotation : Node
{
    public string Name { get; init; } = "";
    public ImmutableArray<TypeAnnotation> TypeArguments { get; init; } = ImmutableArray<TypeAnnotation>.Empty;

    /// <summary>
    /// True if this type uses T? syntax (desugars to Optional[T]).
    /// This is distinct from IsCSharpNullable (T | None for C# interop).
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// True if this type uses T | None syntax (C# nullable interop).
    /// This is distinct from IsOptional (T? syntax for Optional[T]).
    /// </summary>
    public bool IsCSharpNullable { get; init; }

    /// <summary>
    /// The error type E in T !E syntax (desugars to Result[T, E]).
    /// Null if this is not a result type.
    /// </summary>
    public TypeAnnotation? ErrorType { get; init; }

    /// <summary>
    /// True if this type uses T !E syntax (desugars to Result[T, E]).
    /// </summary>
    public bool IsResult => ErrorType != null;

    /// <summary>
    /// Element names for named tuple type annotations (e.g., tuple[x: float, y: float]).
    /// Empty for non-tuple types or unnamed tuples.
    /// When present, must have the same count as TypeArguments.
    /// </summary>
    public ImmutableArray<string?> TupleElementNames { get; init; } = ImmutableArray<string?>.Empty;

    // Source location (LineStart/ColumnStart/LineEnd/ColumnEnd/Span) is inherited from Node.
}

/// <summary>
/// Function type annotation ((int, str) -> bool)
/// </summary>
public record FunctionType
{
    public ImmutableArray<TypeAnnotation> ParameterTypes { get; init; } = ImmutableArray<TypeAnnotation>.Empty;
    public TypeAnnotation ReturnType { get; init; } = null!;

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }

    /// <summary>
    /// Character offset-based span. May be null if not tracked.
    /// </summary>
    public Text.TextSpan? Span { get; init; }
}

/// <summary>
/// Tuple type annotation (tuple[int, str, float] or tuple[x: float, y: float])
/// </summary>
public record TupleType
{
    public ImmutableArray<TypeAnnotation> ElementTypes { get; init; } = ImmutableArray<TypeAnnotation>.Empty;

    /// <summary>
    /// Element names for named tuples. Empty for unnamed tuples.
    /// When present, must have the same count as ElementTypes.
    /// Null entries indicate unnamed elements (not allowed when any are named).
    /// </summary>
    public ImmutableArray<string?> ElementNames { get; init; } = ImmutableArray<string?>.Empty;
}
