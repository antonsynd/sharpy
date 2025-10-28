namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Type annotation
/// </summary>
public record TypeAnnotation
{
    public string Name { get; init; } = string.Empty;
    public List<TypeAnnotation> TypeArguments { get; init; } = new();
    public bool IsOptional { get; init; }
}
