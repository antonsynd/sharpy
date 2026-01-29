using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Shared utility for converting TypeAnnotation to string representation.
/// Used by both OperatorSignatureValidator and ProtocolSignatureValidator.
/// </summary>
public static class TypeAnnotationHelper
{
    /// <summary>
    /// Gets a readable string representation of a type annotation,
    /// handling generic types with type arguments and nullable types.
    /// </summary>
    /// <param name="typeAnnotation">The type annotation to convert, or null for void.</param>
    /// <returns>String representation (e.g., "int", "list[int]", "str?").</returns>
    public static string GetName(TypeAnnotation? typeAnnotation)
    {
        if (typeAnnotation == null)
            return "void";

        var baseName = typeAnnotation.TypeArguments.Length > 0
            ? $"{typeAnnotation.Name}[{string.Join(", ", typeAnnotation.TypeArguments.Select(GetName))}]"
            : typeAnnotation.Name;

        return typeAnnotation.IsOptional ? $"{baseName}?" : baseName;
    }
}
