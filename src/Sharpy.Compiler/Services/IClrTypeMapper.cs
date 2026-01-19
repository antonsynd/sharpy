using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Service for mapping between Sharpy types and CLR types.
/// Uses caching for frequently accessed CLR type information.
/// </summary>
public interface IClrTypeMapper
{
    /// <summary>
    /// Get the CLR Type for a semantic type.
    /// </summary>
    Type? GetClrType(SemanticType semanticType);

    /// <summary>
    /// Get the semantic type for a CLR Type.
    /// </summary>
    SemanticType GetSemanticType(Type clrType);

    /// <summary>
    /// Check if a CLR type has a specific member (method, property, field).
    /// Results are cached.
    /// </summary>
    bool HasMember(Type clrType, string memberName);

    /// <summary>
    /// Get member information from a CLR type.
    /// Results are cached.
    /// </summary>
    System.Reflection.MemberInfo? GetMember(Type clrType, string memberName);
}
