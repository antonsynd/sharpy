using System.Collections.Immutable;

namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Type annotation (int, list[str], dict[str, int], Optional[T], etc.)
/// </summary>
public record TypeAnnotation
{
    public string Name { get; init; } = "";
    public ImmutableArray<TypeAnnotation> TypeArguments { get; init; } = ImmutableArray<TypeAnnotation>.Empty;
    public bool IsNullable { get; init; }  // T? syntax

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
/// Tuple type annotation (tuple[int, str, float])
/// </summary>
public record TupleType
{
    public ImmutableArray<TypeAnnotation> ElementTypes { get; init; } = ImmutableArray<TypeAnnotation>.Empty;
}
