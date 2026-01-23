using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Registry of builtin types and functions from Sharpy.Core
/// Now uses cached reflection-based discovery for functions.
/// </summary>
public class BuiltinRegistry
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

    public BuiltinRegistry()
    {
        _discovery = new CachedModuleDiscovery();
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

        // Collections (generic) - not in PrimitiveCatalog
        // NOTE: These use Sharpy.Core types, not System.Collections.Generic!
        RegisterType("list", typeof(Sharpy.Core.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
        RegisterType("dict", typeof(Sharpy.Core.Dict<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
        RegisterType("set", typeof(Sharpy.Core.Set<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);

        // Special
        RegisterType("object", typeof(object), TypeKind.Class);
        RegisterType("None", typeof(void), TypeKind.Struct); // void for return type

        // Load builtin functions using reflection-based discovery
        LoadBuiltinFunctions();
    }

    private void LoadBuiltinFunctions()
    {
        // Load Sharpy.Core assembly and discover all builtin functions automatically
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly;
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

    public IEnumerable<(string Name, TypeSymbol Type)> GetAllTypes() => _types.Select(kv => (kv.Key, kv.Value));
    public IEnumerable<(string Name, FunctionSymbol Function)> GetAllFunctions() =>
        _functions.SelectMany(kv => kv.Value.Select(f => (kv.Key, f)));
}
