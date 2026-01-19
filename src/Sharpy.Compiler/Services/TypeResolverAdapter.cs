using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Adapter that wraps the existing TypeResolver to implement ITypeResolver.
/// This enables gradual migration to the new services architecture.
/// </summary>
public class TypeResolverAdapter : ITypeResolver
{
    private readonly TypeResolver _typeResolver;

    public TypeResolverAdapter(TypeResolver typeResolver)
    {
        _typeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
    }

    public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
    {
        return _typeResolver.ResolveTypeAnnotation(annotation);
    }

    public IReadOnlyList<SemanticError> Errors => _typeResolver.Errors;

    /// <summary>
    /// Get the underlying TypeResolver for cases that need direct access.
    /// Use sparingly - prefer the interface methods.
    /// </summary>
    public TypeResolver UnderlyingResolver => _typeResolver;
}
