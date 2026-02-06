extern alias SharpyRT;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Registry of builtin types and functions from Sharpy.Core
/// Now uses cached reflection-based discovery for functions.
/// </summary>
internal class BuiltinRegistry
{
    private readonly Dictionary<string, TypeSymbol> _types = new();
    private readonly Dictionary<string, List<FunctionSymbol>> _functions = new();
    private readonly CachedModuleDiscovery _discovery;

    /// <summary>
    /// Primitive types to register from PrimitiveCatalog.
    /// This maintains backward compatibility with the original hard-coded type list.
    /// </summary>
    private static readonly HashSet<string> RegisteredPrimitiveNames = new()
    {
        "int", "long", "float", "double", "decimal", "bool", "str"
    };

    /// <summary>
    /// Tagged union constructor names that the type checker handles via expected type inference.
    /// These are not regular functions — the type checker recognizes them based on context.
    /// </summary>
    private static readonly HashSet<string> TaggedUnionConstructors = new()
    {
        "Some", "Ok", "Err"
    };

    public BuiltinRegistry(ICompilerLogger? logger = null)
    {
        _discovery = new CachedModuleDiscovery(null, logger);
        LoadBuiltins();
    }

    private void LoadBuiltins()
    {
        // Register primitives from PrimitiveCatalog using the defined set of names
        foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
        {
            if (!RegisteredPrimitiveNames.Contains(name))
                continue;
            // Skip void - it's registered separately below
            if (info.ClrType == typeof(void))
                continue;

            var kind = info.ClrType.IsValueType ? TypeKind.Struct : TypeKind.Class;
            RegisterType(info.SharpyName, info.ClrType, kind);
        }

        // Collections (generic) - v0.1.x uses .NET types directly per phases.md
        // Sharpy.Core wrapper types will be introduced in v0.2.x+
        RegisterType("list", typeof(System.Collections.Generic.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
        RegisterType("dict", typeof(System.Collections.Generic.Dictionary<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
        RegisterType("set", typeof(System.Collections.Generic.HashSet<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);

        // Special
        RegisterType("object", typeof(object), TypeKind.Class);
        RegisterType("None", typeof(void), TypeKind.Struct); // void for return type

        // Load builtin functions using reflection-based discovery
        LoadBuiltinFunctions();

        // Auto-discover and register public types from Sharpy.Core (exceptions, etc.)
        LoadBuiltinTypes();

        // Register System.Exception as a base type for catch clauses
        if (!_types.ContainsKey("Exception"))
        {
            RegisterType("Exception", typeof(System.Exception), TypeKind.Class);
        }
    }

    private void LoadBuiltinTypes()
    {
        var discoveredTypes = _discovery.GetModuleTypes("builtins");
        foreach (var typeSymbol in discoveredTypes)
        {
            // Skip types already registered (primitives, collections, etc.)
            if (_types.ContainsKey(typeSymbol.Name))
                continue;

            _types[typeSymbol.Name] = typeSymbol;
        }
    }

    private void LoadBuiltinFunctions()
    {
        // Load Sharpy.Core assembly and discover all builtin functions automatically
        var sharpyCoreAssembly = typeof(SharpyRT::Sharpy.Builtins).Assembly;
        _discovery.LoadAssembly(sharpyCoreAssembly);

        // Get all functions from the "builtins" module
        var builtinFunctions = _discovery.GetModuleFunctions("builtins");

        // Register them in our internal dictionary
        // Note: This is called during construction, so no concurrent access is expected here
        foreach (var function in builtinFunctions)
        {
            if (!_functions.ContainsKey(function.Name))
            {
                _functions[function.Name] = new List<FunctionSymbol>();
            }
            _functions[function.Name].Add(function);
        }
    }

    private void RegisterType(string sharpyName, Type clrType, TypeKind kind, bool isGeneric = false, int typeParamCount = 0)
    {
        var typeSymbol = new TypeSymbol
        {
            Name = sharpyName,
            Kind = SymbolKind.Type,
            TypeKind = kind,
            ClrType = clrType,
            TypeParameters = isGeneric
                ? Enumerable.Range(0, typeParamCount).Select(i => new TypeParameterDef { Name = $"T{i}" }).ToList()
                : new List<TypeParameterDef>(),
            AccessLevel = AccessLevel.Public
        };

        _types[sharpyName] = typeSymbol;
    }

    public TypeSymbol? GetType(string name) => _types.GetValueOrDefault(name);

    /// <summary>
    /// Returns the first function symbol with the given name.
    /// For functions with multiple overloads, use GetFunctionOverloads instead.
    /// </summary>
    public FunctionSymbol? GetFunction(string name) => _functions.GetValueOrDefault(name)?.FirstOrDefault();

    /// <summary>
    /// Returns all function overloads with the given name, or null if no function with that name exists.
    /// </summary>
    public List<FunctionSymbol>? GetFunctionOverloads(string name) => _functions.GetValueOrDefault(name);

    /// <summary>
    /// Returns true if the name is a tagged union constructor (Some, Ok, Err).
    /// These are handled by the type checker via expected type inference, not as regular functions.
    /// </summary>
    public bool IsTaggedUnionConstructor(string name) => TaggedUnionConstructors.Contains(name);

    public IEnumerable<(string Name, TypeSymbol Type)> GetAllTypes() => _types.Select(kv => (kv.Key, kv.Value));
    public IEnumerable<(string Name, FunctionSymbol Function)> GetAllFunctions() =>
        _functions.SelectMany(kv => kv.Value.Select(f => (kv.Key, f)));

    #region CLR Type Fallback

    private readonly Dictionary<string, TypeSymbol?> _clrTypeCache = new();

    /// <summary>
    /// Attempts to resolve a type name as a .NET type from well-known namespaces.
    /// Used as a fallback when a type is not found in the symbol table.
    /// Results are cached for performance.
    /// </summary>
    public TypeSymbol? TryResolveClrType(string name)
    {
        if (_clrTypeCache.TryGetValue(name, out var cached))
            return cached;

        var clrType = TryFindClrType(name);
        if (clrType == null)
        {
            _clrTypeCache[name] = null;
            return null;
        }

        var kind = clrType.IsValueType ? TypeKind.Struct : TypeKind.Class;
        var typeSymbol = new TypeSymbol
        {
            Name = name,
            Kind = SymbolKind.Type,
            TypeKind = kind,
            ClrType = clrType,
            AccessLevel = AccessLevel.Public,
            IsAbstract = clrType.IsAbstract && !clrType.IsInterface
        };

        _clrTypeCache[name] = typeSymbol;
        return typeSymbol;
    }

    private static Type? TryFindClrType(string name)
    {
        // Search well-known namespaces (ordered by likelihood of use in Sharpy)
        string[] namespaces =
        {
            "Sharpy",
            "System",
            "System.Collections.Generic",
            "System.IO",
            "System.Text"
        };

        foreach (var ns in namespaces)
        {
            var fullName = $"{ns}.{name}";
            var type = Type.GetType(fullName);
            if (type != null)
                return type;
        }

        // Search loaded assemblies for types not in System.Private.CoreLib
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var ns in namespaces)
            {
                var type = assembly.GetType($"{ns}.{name}");
                if (type != null)
                    return type;
            }
        }

        return null;
    }

    #endregion
}
