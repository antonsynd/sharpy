extern alias SharpyRT;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Registry;

/// <summary>
/// Registry of builtin types and functions from Sharpy.Core
/// Now uses cached reflection-based discovery for functions.
/// </summary>
[NotThreadSafe(Reason = "Uses non-concurrent Dictionary caches; create per-compilation instance")]
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
        // Load Sharpy.Core assembly first so discovery data is available for RegisterType
        var sharpyCoreAssembly = typeof(SharpyRT::Sharpy.Builtins).Assembly;
        _discovery.LoadAssembly(sharpyCoreAssembly);

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

        // Collections (generic) - use Sharpy.Core wrapper types
        RegisterType("list", typeof(SharpyRT::Sharpy.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
        RegisterType("dict", typeof(System.Collections.Generic.Dictionary<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
        RegisterType("set", typeof(SharpyRT::Sharpy.Set<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);

        // Tuple: registered for OperatorValidator/ProtocolValidator metadata lookup.
        // typeParamCount=1 is nominal — real tuple arity is tracked by TupleType.ElementTypes,
        // not by this TypeSymbol's TypeParameters. CLR type is System.ValueTuple (non-generic sentinel).
        RegisterType(BuiltinNames.Tuple, typeof(System.ValueTuple), TypeKind.Struct, isGeneric: true, typeParamCount: 1);

        // Dict view types (returned by dict.items(), .keys(), .values())
        // ClrType is typeof(object) as a placeholder — codegen resolves actual Sharpy.Core types
        // via CSharpTypeNames. Registered here for protocol validation metadata only.
        RegisterType(BuiltinNames.DictItemsView, typeof(object), TypeKind.Class, isGeneric: true, typeParamCount: 2);
        RegisterType(BuiltinNames.DictKeyView, typeof(object), TypeKind.Class, isGeneric: true, typeParamCount: 2);
        RegisterType(BuiltinNames.DictValuesView, typeof(object), TypeKind.Class, isGeneric: true, typeParamCount: 2);

        // Iterator/iterable types (used by generators and reversed())
        // ClrType is typeof(object) as a placeholder — codegen resolves via CSharpTypeNames.
        RegisterType(BuiltinNames.Iterator, typeof(object), TypeKind.Class, isGeneric: true, typeParamCount: 1);
        RegisterType(BuiltinNames.IEnumerable, typeof(System.Collections.IEnumerable), TypeKind.Interface, isGeneric: true, typeParamCount: 1);
        RegisterType(BuiltinNames.IEnumerator, typeof(System.Collections.IEnumerator), TypeKind.Interface, isGeneric: true, typeParamCount: 1);

        // Result and Optional (for semantic-time method/property resolution)
        RegisterType("Result", typeof(SharpyRT::Sharpy.Result<,>), TypeKind.Struct, isGeneric: true, typeParamCount: 2);
        RegisterType("Optional", typeof(SharpyRT::Sharpy.Optional<>), TypeKind.Struct, isGeneric: true, typeParamCount: 1);

        // Special
        RegisterType("object", typeof(object), TypeKind.Class);
        RegisterType("None", typeof(void), TypeKind.Struct); // void for return type

        // Load builtin functions using reflection-based discovery
        LoadBuiltinFunctions();

        // Generic builtins (reversed, sorted, min, max) are now auto-discovered via
        // reflection instead of manual RegisterGenericBuiltin() calls. Type inference
        // for their return types is handled by TypeChecker special cases.

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
        // Get all functions from the "builtins" module (assembly already loaded in LoadBuiltins)
        var builtinFunctions = _discovery.GetModuleFunctions("builtins");

        // Register them in our internal dictionary
        // Note: This is called during construction, so no concurrent access is expected here
        foreach (var function in builtinFunctions)
        {
            // Skip generic functions whose name collides with a registered type constructor.
            // This specifically prevents CLR-discovered generic overloads like Builtins.List<T>(),
            // Builtins.Bool<T>(), Builtins.Int<T>() from shadowing the type constructors
            // registered by RegisterTypeConstructor(). User-defined types cannot collide here
            // because _types only contains compiler-registered builtin type names.
            if (function.IsGeneric && _types.ContainsKey(function.Name))
                continue;

            if (!_functions.ContainsKey(function.Name))
            {
                _functions[function.Name] = new List<FunctionSymbol>();
            }
            _functions[function.Name].Add(function);
        }
    }

    private void RegisterType(string sharpyName, Type clrType, TypeKind kind, bool isGeneric = false, int typeParamCount = 0)
    {
        var typeParams = isGeneric
            ? Enumerable.Range(0, typeParamCount).Select(i => new TypeParameterDef { Name = $"T{i}" }).ToList()
            : new List<TypeParameterDef>();

        // Build shared TypeParameterType instances for generic types so all methods
        // reference the same objects (required for reference equality in substitution).
        var sharedTypeParams = isGeneric
            ? typeParams.Select(tp => new TypeParameterType { Name = tp.Name }).ToArray()
            : Array.Empty<TypeParameterType>();

        // Discover methods, operators, and protocols from Sharpy.Core via CLR reflection.
        var discovered = _discovery.GetTypeByName(sharpyName, sharedTypeParams);

        var methods = discovered?.Methods ?? new List<FunctionSymbol>();
        var operatorMethods = discovered?.OperatorMethods ?? new Dictionary<string, List<FunctionSymbol>>();
        var protocolMethods = discovered?.ProtocolMethods ?? new Dictionary<string, List<FunctionSymbol>>();

        // For types not discoverable from Sharpy.Core, provide inline definitions.
        ApplyNonDiscoverableDefinitions(sharpyName, ref methods, ref operatorMethods, ref protocolMethods);

        var typeSymbol = new TypeSymbol
        {
            Name = sharpyName,
            Kind = SymbolKind.Type,
            TypeKind = kind,
            ClrType = clrType,
            TypeParameters = typeParams,
            AccessLevel = AccessLevel.Public,
            IsCovariant = IsCovariant(sharpyName),
            Methods = methods,
            OperatorMethods = operatorMethods,
            ProtocolMethods = protocolMethods,
        };

        PopulateMethodOverloads(typeSymbol);
        _types[sharpyName] = typeSymbol;
    }

    /// <summary>
    /// Populates MethodOverloads on a TypeSymbol for methods that share the same name.
    /// </summary>
    private static void PopulateMethodOverloads(TypeSymbol typeSymbol)
    {
        var overloadGroups = typeSymbol.Methods
            .GroupBy(m => m.Name)
            .Where(g => g.Count() > 1);

        foreach (var group in overloadGroups)
        {
            typeSymbol.MethodOverloads[group.Key] = group.ToList();
        }
    }

    /// <summary>
    /// Provides methods, operators, and protocols for types that cannot be discovered
    /// <summary>
    /// Provides inline definitions for types whose methods, operators, or protocols cannot
    /// be discovered from Sharpy.Core via CLR reflection. Each case here is permanent by design:
    /// <list type="bullet">
    /// <item><description>
    /// <b>tuple</b>: Maps to <c>System.ValueTuple</c>, whose operators (<c>==</c>, <c>+</c>, <c>*</c>)
    /// are compiler-synthesized by Roslyn/CLR, not present as discoverable CLR methods.
    /// Protocols (<c>__len__</c>, <c>__iter__</c>, <c>__getitem__</c>) similarly have no CLR surface.
    /// </description></item>
    /// <item><description>
    /// <b>Iterator/IEnumerable/IEnumerator</b>: Abstract placeholder types registered with
    /// <c>typeof(object)</c>. They have no real CLR type surface — only the <c>__iter__</c>
    /// protocol stub is needed for type-checking iteration patterns.
    /// </description></item>
    /// <item><description>
    /// <b>int.parse / float.parse</b>: Live on separate utility classes (<c>IntParse</c>,
    /// <c>DoubleParse</c>) in Sharpy.Core, not on <c>System.Int32</c> / <c>System.Double</c>.
    /// Discovery operates on the actual CLR type surface, so these cross-type helpers
    /// must be registered manually as static methods on the Sharpy <c>int</c>/<c>float</c> types.
    /// </description></item>
    /// </list>
    /// </summary>
    private static void ApplyNonDiscoverableDefinitions(
        string typeName,
        ref List<FunctionSymbol> methods,
        ref Dictionary<string, List<FunctionSymbol>> operatorMethods,
        ref Dictionary<string, List<FunctionSymbol>> protocolMethods)
    {
        switch (typeName)
        {
            case BuiltinNames.Tuple:
                operatorMethods = MakeDunderDict(DunderNames.Add, DunderNames.Mul, DunderNames.Eq, DunderNames.Ne);
                protocolMethods = MakeDunderDict(DunderNames.Len, DunderNames.Iter, DunderNames.GetItem);
                break;

            case BuiltinNames.Iterator or BuiltinNames.IEnumerable or BuiltinNames.IEnumerator:
                protocolMethods = MakeDunderDict(DunderNames.Iter);
                break;

            case BuiltinNames.Int:
                if (methods.Count == 0)
                    methods = new List<FunctionSymbol> { MakeParseMethod(SemanticType.Int) };
                else
                    methods.Add(MakeParseMethod(SemanticType.Int));
                break;

            case BuiltinNames.Float:
                if (methods.Count == 0)
                    methods = new List<FunctionSymbol> { MakeParseMethod(SemanticType.Float) };
                else
                    methods.Add(MakeParseMethod(SemanticType.Float));
                break;
        }
    }

    private static readonly UserDefinedType ValueErrorType = new() { Name = "ValueError" };

    private static FunctionSymbol MakeParseMethod(SemanticType resultOkType)
    {
        return new FunctionSymbol
        {
            Name = "parse",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "s", Type = SemanticType.Str }
            },
            ReturnType = new ResultType { OkType = resultOkType, ErrorType = ValueErrorType },
            AccessLevel = AccessLevel.Public,
            IsStatic = true,
        };
    }

    /// <summary>
    /// Creates a dictionary of dunder names, each with a single placeholder FunctionSymbol.
    /// Used for operator and protocol stubs where validators only check key presence.
    /// </summary>
    private static Dictionary<string, List<FunctionSymbol>> MakeDunderDict(params string[] dunderNames)
    {
        var dict = new Dictionary<string, List<FunctionSymbol>>();
        foreach (var name in dunderNames)
        {
            dict[name] = new List<FunctionSymbol>
            {
                new FunctionSymbol
                {
                    Name = name,
                    Kind = SymbolKind.Function,
                    AccessLevel = AccessLevel.Public,
                }
            };
        }
        return dict;
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

    /// <summary>
    /// Returns whether the given builtin type is covariant in its type parameters.
    /// </summary>
    public static bool IsCovariant(string typeName) => typeName is BuiltinNames.List or BuiltinNames.Set;

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
