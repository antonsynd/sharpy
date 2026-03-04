using System.Text.Json.Serialization;

namespace Sharpy.Compiler.Discovery.Caching;

/// <summary>
/// Serializable index of function overloads discovered from an assembly.
/// </summary>
internal class OverloadIndex
{
    public AssemblyIdentity Identity { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CacheFormatVersion { get; set; } = 6;
    public Dictionary<string, ModuleOverloads> Modules { get; set; } = new();
}

/// <summary>
/// Overloads for functions in a module.
/// </summary>
internal class ModuleOverloads
{
    public string ModuleName { get; set; } = string.Empty;
    public Dictionary<string, List<FunctionSignature>> Functions { get; set; } = new();
    public List<DiscoveredTypeInfo> Types { get; set; } = new();
}

/// <summary>
/// Information about a public type discovered from an assembly.
/// </summary>
internal class DiscoveredTypeInfo
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string ClrTypeName { get; set; } = string.Empty;
    public bool IsException { get; set; }
    public string? BaseTypeName { get; set; }
    public string TypeKind { get; set; } = "Class";
    public List<FunctionSignature> Methods { get; set; } = new();
    public Dictionary<string, List<FunctionSignature>> OperatorMethods { get; set; } = new();
    public Dictionary<string, List<FunctionSignature>> ProtocolMethods { get; set; } = new();
}

/// <summary>
/// Signature of a single function overload.
/// </summary>
internal class FunctionSignature
{
    public string Name { get; set; } = string.Empty;
    public List<ParameterSignature> Parameters { get; set; } = new();
    public TypeSignature ReturnType { get; set; } = new();

    /// <summary>
    /// Generic type parameter names (e.g., ["T"] for Min&lt;T&gt;).
    /// Empty for non-generic methods.
    /// </summary>
    public List<string> TypeParameters { get; set; } = new();

    /// <summary>
    /// Method reference for rehydration: AssemblyName|TypeName|MethodName|ParamCount
    /// </summary>
    public string MethodToken { get; set; } = string.Empty;
}

/// <summary>
/// Signature of a parameter.
/// </summary>
internal class ParameterSignature
{
    public string Name { get; set; } = string.Empty;
    public TypeSignature Type { get; set; } = new();
    public bool HasDefault { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsVariadic { get; set; }
}

/// <summary>
/// Signature of a type.
/// </summary>
internal class TypeSignature
{
    public string Name { get; set; } = string.Empty;
    public bool IsGeneric { get; set; }
    public List<TypeSignature> TypeArguments { get; set; } = new();

    /// <summary>
    /// True if this type references a generic type parameter (e.g., T in Min&lt;T&gt;).
    /// </summary>
    public bool IsGenericParameter { get; set; }

    /// <summary>
    /// Positional index of the generic parameter on the declaring type (0-based).
    /// Only meaningful when <see cref="IsGenericParameter"/> is true.
    /// Maps to <see cref="Type.GenericParameterPosition"/> from CLR reflection.
    /// </summary>
    public int GenericParameterPosition { get; set; }

    /// <summary>
    /// CLR type name for mapping back.
    /// </summary>
    public string ClrTypeName { get; set; } = string.Empty;
}
