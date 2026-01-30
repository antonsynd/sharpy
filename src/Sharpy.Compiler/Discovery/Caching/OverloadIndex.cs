using System.Text.Json.Serialization;

namespace Sharpy.Compiler.Discovery.Caching;

/// <summary>
/// Serializable index of function overloads discovered from an assembly.
/// </summary>
public class OverloadIndex
{
    public AssemblyIdentity Identity { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CacheFormatVersion { get; set; } = 1;
    public Dictionary<string, ModuleOverloads> Modules { get; set; } = new();
}

/// <summary>
/// Overloads for functions in a module.
/// </summary>
public class ModuleOverloads
{
    public string ModuleName { get; set; } = string.Empty;
    public Dictionary<string, List<FunctionSignature>> Functions { get; set; } = new();
    public List<DiscoveredTypeInfo> Types { get; set; } = new();
}

/// <summary>
/// Information about a public type discovered from an assembly.
/// </summary>
public class DiscoveredTypeInfo
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string ClrTypeName { get; set; } = string.Empty;
    public bool IsException { get; set; }
    public string? BaseTypeName { get; set; }
    public string TypeKind { get; set; } = "Class";
}

/// <summary>
/// Signature of a single function overload.
/// </summary>
public class FunctionSignature
{
    public string Name { get; set; } = string.Empty;
    public List<ParameterSignature> Parameters { get; set; } = new();
    public TypeSignature ReturnType { get; set; } = new();

    /// <summary>
    /// Method reference for rehydration: AssemblyName|TypeName|MethodName|ParamCount
    /// </summary>
    public string MethodToken { get; set; } = string.Empty;
}

/// <summary>
/// Signature of a parameter.
/// </summary>
public class ParameterSignature
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
public class TypeSignature
{
    public string Name { get; set; } = string.Empty;
    public bool IsGeneric { get; set; }
    public List<TypeSignature> TypeArguments { get; set; } = new();

    /// <summary>
    /// CLR type name for mapping back.
    /// </summary>
    public string ClrTypeName { get; set; } = string.Empty;
}
