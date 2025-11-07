namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Type annotation (int, list[str], dict[str, int], Optional[T], etc.)
/// </summary>
public record TypeAnnotation
{
    public string Name { get; init; } = "";
    public List<TypeAnnotation> TypeArguments { get; init; } = new();
    public bool IsNullable { get; init; }  // T? syntax

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}

/// <summary>
/// Function type annotation ((int, str) -> bool)
/// </summary>
public record FunctionType
{
    public List<TypeAnnotation> ParameterTypes { get; init; } = new();
    public TypeAnnotation ReturnType { get; init; } = null!;
}

/// <summary>
/// Tuple type annotation (tuple[int, str, float])
/// </summary>
public record TupleType
{
    public List<TypeAnnotation> ElementTypes { get; init; } = new();
}
