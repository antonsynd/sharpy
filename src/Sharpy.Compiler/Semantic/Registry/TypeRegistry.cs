namespace Sharpy.Compiler.Semantic.Registry;

/// <summary>
/// Central registry for type information.
/// Provides canonical access to types and caches resolved type info.
///
/// This addresses the problem of type information being scattered across
/// TypeAnnotation, SemanticType, and TypeSymbol.
/// </summary>
internal class TypeRegistry
{
    private readonly Dictionary<string, SemanticType> _builtinTypes = new();
    private readonly Dictionary<string, TypeSymbol> _userTypes = new();
    private readonly SymbolTable _symbolTable;

    public TypeRegistry(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable;
        InitializeBuiltinTypes();
    }

    private void InitializeBuiltinTypes()
    {
        // Register canonical builtin types
        _builtinTypes["int"] = SemanticType.Int;
        _builtinTypes["int32"] = SemanticType.Int;
        _builtinTypes["long"] = SemanticType.Long;
        _builtinTypes["int64"] = SemanticType.Long;
        _builtinTypes["float"] = SemanticType.Float;
        _builtinTypes["float64"] = SemanticType.Float;
        _builtinTypes["float32"] = SemanticType.Float32;
        _builtinTypes["double"] = SemanticType.Double;
        _builtinTypes["bool"] = SemanticType.Bool;
        _builtinTypes["str"] = SemanticType.Str;
        _builtinTypes["string"] = SemanticType.Str;
        _builtinTypes["object"] = SemanticType.Object;
    }

    /// <summary>
    /// Get a type by name, checking builtins first, then user-defined.
    /// </summary>
    public SemanticType? GetType(string name)
    {
        if (_builtinTypes.TryGetValue(name, out var builtin))
            return builtin;

        var symbol = _symbolTable.Lookup(name);
        if (symbol is TypeSymbol typeSymbol)
        {
            return new UserDefinedType { Name = name, Symbol = typeSymbol };
        }

        return null;
    }

    /// <summary>
    /// Get a builtin type by name. Returns null if not a builtin.
    /// </summary>
    public SemanticType? GetBuiltinType(string name)
    {
        return _builtinTypes.TryGetValue(name, out var builtin) ? builtin : null;
    }

    /// <summary>
    /// Register a user-defined type.
    /// Called during NameResolver when type definitions are encountered.
    /// </summary>
    public void RegisterType(TypeSymbol typeSymbol)
    {
        _userTypes[typeSymbol.Name] = typeSymbol;
    }

    /// <summary>
    /// Check if a type name is a builtin type.
    /// </summary>
    public bool IsBuiltinType(string name) => _builtinTypes.ContainsKey(name);

    /// <summary>
    /// Check if a type name is registered as a user-defined type.
    /// </summary>
    public bool IsUserDefinedType(string name) => _userTypes.ContainsKey(name);

    /// <summary>
    /// Get all registered user-defined types.
    /// </summary>
    public IEnumerable<TypeSymbol> GetUserDefinedTypes() => _userTypes.Values;

    /// <summary>
    /// Get all registered builtin types.
    /// </summary>
    public IEnumerable<SemanticType> GetBuiltinTypes() => _builtinTypes.Values.Distinct();

    /// <summary>
    /// Check if two types are the same (structural equality).
    /// </summary>
    public static bool AreEqual(SemanticType a, SemanticType b) => a.Equals(b);

    /// <summary>
    /// Create a nullable version of a type.
    /// </summary>
    public static SemanticType MakeNullable(SemanticType type)
    {
        if (type is NullableType)
            return type;
        return new NullableType { UnderlyingType = type };
    }

    /// <summary>
    /// Unwrap a nullable type. Returns the type unchanged if not nullable.
    /// </summary>
    public static SemanticType UnwrapNullable(SemanticType type)
    {
        if (type is NullableType nullable)
            return nullable.UnderlyingType;
        return type;
    }
}
