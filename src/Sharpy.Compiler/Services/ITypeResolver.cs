using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Service for resolving type annotations to semantic types.
/// Thread-safe for parallel compilation scenarios.
/// </summary>
public interface ITypeResolver
{
    /// <summary>
    /// Resolves a type annotation to its semantic type.
    /// Results are cached for efficiency.
    /// </summary>
    SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation);

    /// <summary>
    /// Gets diagnostics that occurred during type resolution.
    /// </summary>
    DiagnosticBag Diagnostics { get; }
}
