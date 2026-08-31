using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Shared utility for converting TypeAnnotation to string representation.
/// Used by SignatureValidator for dunder signature validation.
/// </summary>
internal static class TypeAnnotationHelper
{
    /// <summary>
    /// Gets a readable string representation of a type annotation, quoting the user's own
    /// spelling: generic arguments, `T?` (Optional), `T | None` (nullable), and `T !E` (Result)
    /// must each survive the round-trip — a diagnostic that quotes `int` for an `int | None`
    /// annotation misreports what the user wrote (#1714 class, annotation surface).
    /// </summary>
    /// <param name="typeAnnotation">The type annotation to convert, or null for void.</param>
    /// <returns>String representation (e.g., "int", "list[int]", "str?", "str | None").</returns>
    public static string GetName(TypeAnnotation? typeAnnotation)
    {
        if (typeAnnotation == null)
            return "void";

        var name = typeAnnotation.TypeArguments.Length > 0
            ? $"{typeAnnotation.Name}[{string.Join(", ", typeAnnotation.TypeArguments.Select(GetName))}]"
            : typeAnnotation.Name;

        if (typeAnnotation.IsOptional)
            name = $"{name}?";
        if (typeAnnotation.IsCSharpNullable)
            name = $"{name} | None";
        if (typeAnnotation.IsResult)
            name = $"{name} !{GetName(typeAnnotation.ErrorType)}";

        return name;
    }
}
