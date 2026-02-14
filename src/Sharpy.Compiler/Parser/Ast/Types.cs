using System.Collections.Immutable;

namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Type annotation (int, list[str], dict[str, int], Optional[T], etc.)
/// </summary>
public record TypeAnnotation
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
