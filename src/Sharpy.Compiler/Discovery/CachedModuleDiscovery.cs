using System.Collections.Concurrent;
using System.Reflection;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Discovers and caches function overloads from assemblies.
/// Uses reflection on first load, then caches results for subsequent loads.
/// Thread-safe for concurrent use.
/// </summary>
[ThreadSafe]
internal class CachedModuleDiscovery
{
    private readonly OverloadIndexCache _cache;
    private readonly OverloadIndexBuilder _builder;
    private readonly ClrTypeMapper _typeMapper;
    private readonly ConcurrentDictionary<string, Lazy<OverloadIndex>> _loadedIndices = new();
    private readonly ConcurrentDictionary<string, byte> _moduleTypeNames = new();

    /// <summary>
    /// Create a discovery instance using the default cache directory.
    /// </summary>
    public CachedModuleDiscovery() : this(null, null)
    {
    }

    /// <summary>
    /// Create a discovery instance with a custom cache and logger.
    /// </summary>
    /// <param name="cache">Custom cache instance. If null, creates a default cache.</param>
    /// <param name="logger">Optional logger. If null, uses NullLogger.</param>
    public CachedModuleDiscovery(OverloadIndexCache? cache, ICompilerLogger? logger = null)
    {
        _cache = cache ?? new OverloadIndexCache(null, logger);
        _builder = new OverloadIndexBuilder(logger);
        _typeMapper = new ClrTypeMapper();
    }

    /// <summary>
    /// Load an assembly and discover its functions.
    /// Uses cache if available, otherwise builds and caches.
    /// </summary>
    public void LoadAssembly(Assembly assembly)
    {
        var identity = AssemblyIdentity.FromAssembly(assembly);

        // Use Lazy<T> with GetOrAdd to guarantee single factory execution
        // under contention — ConcurrentDictionary.GetOrAdd may invoke the factory
        // multiple times, but only one Lazy<T>.Value will be evaluated.
        var lazy = _loadedIndices.GetOrAdd(identity.Name, _ => new Lazy<OverloadIndex>(() =>
        {
            // Try to load from cache
            var index = _cache.TryLoad(identity);

            if (index == null)
            {
                // Cache miss - build from reflection
                index = _builder.BuildFromAssembly(assembly);
                _cache.Save(index);
            }

            return index;
        }));

        // Force evaluation so the assembly is loaded eagerly
        var index = lazy.Value;

        // Pre-compute module type names for ConvertTypeSignature
        foreach (var moduleOverloads in index.Modules.Values)
        {
            foreach (var typeInfo in moduleOverloads.Types)
            {
                if (typeInfo.IsModuleType)
                    _moduleTypeNames.TryAdd(typeInfo.Namespace + "." + typeInfo.Name, 0);
            }
        }
    }

    /// <summary>
    /// Get all function symbols from a specific module.
    /// </summary>
    public List<FunctionSymbol> GetModuleFunctions(string moduleName)
    {
        var functions = new List<FunctionSymbol>();

        foreach (var lazy in _loadedIndices.Values)
        {
            var index = lazy.Value;
            if (!index.Modules.TryGetValue(moduleName, out var moduleOverloads))
                continue;

            foreach (var (functionName, signatures) in moduleOverloads.Functions)
            {
                foreach (var signature in signatures)
                {
                    functions.Add(ConvertToFunctionSymbol(signature, moduleName));
                }
            }
        }

        return functions;
    }

    /// <summary>
    /// Get all discovered type symbols from a specific module.
    /// </summary>
    public List<TypeSymbol> GetModuleTypes(string moduleName)
    {
        var types = new List<TypeSymbol>();

        foreach (var lazy in _loadedIndices.Values)
        {
            var index = lazy.Value;
            if (!index.Modules.TryGetValue(moduleName, out var moduleOverloads))
                continue;

            foreach (var typeInfo in moduleOverloads.Types)
            {
                var typeSymbol = ConvertToTypeSymbol(typeInfo);
                if (typeSymbol != null)
                    types.Add(typeSymbol);
            }
        }

        return types;
    }

    /// <summary>
    /// Convert a cached DiscoveredTypeInfo to a TypeSymbol.
    /// </summary>
    private TypeSymbol? ConvertToTypeSymbol(Caching.DiscoveredTypeInfo typeInfo)
    {
        return ConvertToTypeSymbol(typeInfo, sharedTypeParams: null);
    }

    /// <summary>
    /// Convert a cached DiscoveredTypeInfo to a TypeSymbol, optionally remapping generic type
    /// parameters to shared instances.
    /// </summary>
    private TypeSymbol? ConvertToTypeSymbol(
        Caching.DiscoveredTypeInfo typeInfo, TypeParameterType[]? sharedTypeParams)
    {
        // Resolve CLR type
        Type? clrType = Type.GetType(typeInfo.ClrTypeName);
        if (clrType == null)
        {
            clrType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(typeInfo.ClrTypeName))
                .FirstOrDefault(t => t != null);
        }

        var typeKind = typeInfo.TypeKind switch
        {
            "Enum" => TypeKind.Enum,
            "Struct" => TypeKind.Struct,
            "Interface" => TypeKind.Interface,
            _ => TypeKind.Class
        };

        // Convert methods from discovery, expanding default parameters into separate overloads
        var methods = new List<FunctionSymbol>();
        if (typeInfo.Methods.Count > 0)
        {
            // Strip generic arity suffix for OverloadExpander type name matching
            var expanderTypeName = typeInfo.Name;
            var backtick = expanderTypeName.IndexOf('`', StringComparison.Ordinal);
            if (backtick >= 0)
                expanderTypeName = expanderTypeName[..backtick];

            foreach (var sig in typeInfo.Methods)
            {
                var expanded = OverloadExpander.Expand(sig, expanderTypeName);
                foreach (var overloadSig in expanded)
                {
                    methods.Add(ConvertToFunctionSymbol(overloadSig, typeInfo.Name, sharedTypeParams));
                }
            }
        }

        // Convert operator methods from discovery with full signatures preserved.
        // TypeInferenceService.FindBestOverload needs parameter types and return types
        // to resolve operator overloads (e.g., Path / str -> Path).
        var operatorMethods = ConvertOperatorMethods(typeInfo.OperatorMethods, sharedTypeParams);

        // Convert protocol methods from discovery as marker-only stubs (same rationale).
        var protocolMethods = NormalizeToDunderStubs(typeInfo.ProtocolMethods);

        // Convert properties from discovery
        var properties = typeInfo.Properties
            .Select(p => new PropertySymbol
            {
                Name = ReverseNameMangler.ToSharpyName(p.Name, ReverseNameContext.Property),
                Type = ConvertTypeSignature(p.PropertyType, sharedTypeParams),
                HasGetter = p.HasGetter,
                HasSetter = p.HasSetter,
            })
            .ToList();

        return new TypeSymbol
        {
            Name = typeInfo.Name,
            Kind = SymbolKind.Type,
            TypeKind = typeKind,
            ClrType = clrType,
            AccessLevel = AccessLevel.Public,
            IsAbstract = clrType?.IsAbstract == true && !clrType.IsInterface,
            Methods = methods,
            OperatorMethods = operatorMethods,
            ProtocolMethods = protocolMethods,
            Properties = properties,
        };
    }

    /// <summary>
    /// Explicit mapping from Sharpy type names to CLR type names in the discovery index.
    /// Eliminates case-insensitive matching by making the naming contract explicit.
    /// Only types whose Sharpy name differs from the CLR name need entries here.
    /// </summary>
    private static readonly Dictionary<string, string> SharpyToClrNameMap = new()
    {
        ["list"] = "List",
        ["dict"] = "Dict",
        ["set"] = "Set",
        ["tuple"] = "ValueTuple",
    };

    /// <summary>
    /// Get a fully-populated TypeSymbol for a specific builtin type name,
    /// with methods, operators, and protocols populated from discovery.
    /// Returns null if the type is not found in the discovery index.
    /// </summary>
    public TypeSymbol? GetTypeByName(string sharpyName)
    {
        return GetTypeByName(sharpyName, sharedTypeParams: null);
    }

    /// <summary>
    /// Get a fully-populated TypeSymbol for a specific builtin type name,
    /// with methods, operators, and protocols populated from discovery.
    /// When sharedTypeParams is provided, all discovered methods will use
    /// the same TypeParameterType instances for generic type parameters.
    /// Returns null if the type is not found in the discovery index.
    /// </summary>
    public TypeSymbol? GetTypeByName(string sharpyName, TypeParameterType[]? sharedTypeParams)
    {
        // Translate Sharpy name to expected CLR name; fall back to the Sharpy name itself
        var clrName = SharpyToClrNameMap.TryGetValue(sharpyName, out var mapped) ? mapped : sharpyName;

        foreach (var lazy in _loadedIndices.Values)
        {
            var index = lazy.Value;
            foreach (var moduleOverloads in index.Modules.Values)
            {
                // For generic types like List`1, match by stripping the generic arity suffix
                var typeInfo = moduleOverloads.Types.FirstOrDefault(t =>
                {
                    var name = t.Name;
                    // Strip generic arity suffix (e.g., List`1 -> List)
                    var backtickIndex = name.IndexOf('`', StringComparison.Ordinal);
                    if (backtickIndex >= 0)
                        name = name[..backtickIndex];
                    return string.Equals(name, clrName, StringComparison.Ordinal);
                });

                if (typeInfo != null && (typeInfo.Methods.Count > 0 || typeInfo.OperatorMethods.Count > 0 || typeInfo.ProtocolMethods.Count > 0 || typeInfo.Properties.Count > 0))
                {
                    return ConvertToTypeSymbol(typeInfo, sharedTypeParams);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Get all loaded modules.
    /// </summary>
    public IEnumerable<string> GetLoadedModules()
    {
        return _loadedIndices.Values
            .Select(lazy => lazy.Value)
            .SelectMany(index => index.Modules.Keys)
            .Distinct();
    }

    /// <summary>
    /// Clear all cached data.
    /// </summary>
    public void ClearCache()
    {
        _cache.ClearAll();
    }

    /// <summary>
    /// Converts discovered operator methods to FunctionSymbol entries with full signatures.
    /// Preserves parameter types and return type so TypeInferenceService can resolve overloads.
    /// </summary>
    private Dictionary<string, List<FunctionSymbol>> ConvertOperatorMethods(
        Dictionary<string, List<FunctionSignature>> discovered,
        TypeParameterType[]? sharedTypeParams)
    {
        var result = new Dictionary<string, List<FunctionSymbol>>();
        foreach (var (dunderName, signatures) in discovered)
        {
            var symbols = new List<FunctionSymbol>();
            foreach (var sig in signatures)
            {
                symbols.Add(ConvertToFunctionSymbol(sig, dunderName, sharedTypeParams));
            }
            result[dunderName] = symbols;
        }
        return result;
    }

    /// <summary>
    /// Converts discovered dunder methods (operators or protocols) to marker-only stubs.
    /// Each stub retains only Name, Kind, and AccessLevel — Parameters, ReturnType, and
    /// TypeParameters are left at their defaults (empty/null). Validators only check
    /// for key presence, not the actual method signatures.
    /// </summary>
    private static Dictionary<string, List<FunctionSymbol>> NormalizeToDunderStubs(
        Dictionary<string, List<FunctionSignature>> discovered)
    {
        var result = new Dictionary<string, List<FunctionSymbol>>();
        foreach (var (dunderName, _) in discovered)
        {
            result[dunderName] = new List<FunctionSymbol>
            {
                new FunctionSymbol
                {
                    Name = dunderName,
                    Kind = SymbolKind.Function,
                    AccessLevel = AccessLevel.Public,
                }
            };
        }
        return result;
    }

    /// <summary>
    /// Convert a cached FunctionSignature back to a FunctionSymbol.
    /// </summary>
    private FunctionSymbol ConvertToFunctionSymbol(FunctionSignature signature, string moduleName)
    {
        return ConvertToFunctionSymbol(signature, moduleName, sharedTypeParams: null);
    }

    /// <summary>
    /// Convert a cached FunctionSignature back to a FunctionSymbol, remapping generic type
    /// parameters to shared <see cref="TypeParameterType"/> instances when provided.
    /// This ensures all methods on a generic type share the same TypeParameterType objects,
    /// which is required for reference equality during generic substitution.
    /// </summary>
    internal FunctionSymbol ConvertToFunctionSymbol(
        FunctionSignature signature, string moduleName, TypeParameterType[]? sharedTypeParams)
    {
        return new FunctionSymbol
        {
            Name = signature.Name,
            Kind = SymbolKind.Function,
            ReturnType = ConvertTypeSignature(signature.ReturnType, sharedTypeParams),
            Parameters = signature.Parameters
                .Select(p => new ParameterSymbol
                {
                    Name = p.Name,
                    // For variadic parameters, extract the element type from list[T] -> T
                    Type = p.IsVariadic
                        ? GetVariadicElementType(p.Type, sharedTypeParams)
                        : ConvertTypeSignature(p.Type, sharedTypeParams),
                    HasDefault = p.HasDefault,
                    DefaultValue = p.HasDefault ? DefaultValueParser.Parse(p.DefaultValue) : null,
                    IsVariadic = p.IsVariadic
                })
                .ToList(),
            AccessLevel = AccessLevel.Public,
            TypeParameters = signature.TypeParameters.Count > 0
                ? signature.TypeParameters
                    .Select(name => new Parser.Ast.TypeParameterDef { Name = name })
                    .ToList()
                : new List<Parser.Ast.TypeParameterDef>()
        };
    }

    /// <summary>
    /// Extract the element type from a variadic parameter's type signature.
    /// Variadic parameters are stored as list[T] but we need just T for type checking.
    /// </summary>
    private SemanticType GetVariadicElementType(
        TypeSignature typeSignature, TypeParameterType[]? sharedTypeParams = null)
    {
        // Variadic parameters are stored as list[T] (mapped from T[])
        // We need to extract T
        if (typeSignature.IsGeneric && typeSignature.Name.StartsWith("list") && typeSignature.TypeArguments.Count == 1)
        {
            return ConvertTypeSignature(typeSignature.TypeArguments[0], sharedTypeParams);
        }
        // Fallback to the full type if not a list
        return ConvertTypeSignature(typeSignature, sharedTypeParams);
    }

    /// <summary>
    /// Convert a TypeSignature back to a SemanticType.
    /// </summary>
    private SemanticType ConvertTypeSignature(TypeSignature signature)
    {
        return ConvertTypeSignature(signature, sharedTypeParams: null);
    }

    /// <summary>
    /// Convert a TypeSignature back to a SemanticType, optionally remapping generic type
    /// parameters to shared instances by positional index.
    /// </summary>
    private SemanticType ConvertTypeSignature(TypeSignature signature, TypeParameterType[]? sharedTypeParams)
    {
        // Handle generic type parameters (e.g., T in Min<T>)
        if (signature.IsGenericParameter)
        {
            // When shared type params are provided, remap by positional index
            // to ensure all methods share the same TypeParameterType instances.
            if (sharedTypeParams != null)
            {
                var position = signature.GenericParameterPosition;
                if (position >= 0 && position < sharedTypeParams.Length)
                {
                    return sharedTypeParams[position];
                }
            }

            return new TypeParameterType { Name = signature.Name };
        }

        // Handle primitive types
        if (signature.Name == BuiltinNames.Int)
            return SemanticType.Int;
        if (signature.Name == BuiltinNames.Long)
            return SemanticType.Long;
        if (signature.Name == BuiltinNames.Float)
            return SemanticType.Float;       // float -> double (per spec)
        if (signature.Name == BuiltinNames.Float32)
            return SemanticType.Float32;   // float32 -> C# float
        if (signature.Name == BuiltinNames.Float64)
            return SemanticType.Double;    // float64 -> double
        if (signature.Name == BuiltinNames.Double)
            return SemanticType.Double;
        if (signature.Name == BuiltinNames.Bool)
            return SemanticType.Bool;
        if (signature.Name == BuiltinNames.Str)
            return SemanticType.Str;
        if (signature.Name == BuiltinNames.None)
            return SemanticType.Void;
        if (signature.Name == BuiltinNames.Object)
            return SemanticType.Object;

        // Handle generic types
        if (signature.IsGeneric)
        {
            // Handle Optional[T] -> OptionalType (produced by OverloadExpander for dict.get/pop)
            if (signature.Name == BuiltinNames.Optional && signature.TypeArguments.Count == 1)
            {
                return new OptionalType
                {
                    UnderlyingType = ConvertTypeSignature(signature.TypeArguments[0], sharedTypeParams)
                };
            }

            // Handle tuple[T0, T1, ...] -> TupleType
            if (signature.Name == BuiltinNames.Tuple && signature.TypeArguments.Count > 0)
            {
                return new TupleType
                {
                    ElementTypes = signature.TypeArguments
                        .Select(ta => ConvertTypeSignature(ta, sharedTypeParams))
                        .ToList()
                };
            }

            return new GenericType
            {
                // Extract base name before '[' if present; otherwise use the whole name.
                Name = signature.Name.Contains('[', StringComparison.Ordinal)
                    ? signature.Name[..signature.Name.IndexOf('[', StringComparison.Ordinal)]
                    : signature.Name,
                TypeArguments = signature.TypeArguments
                    .Select(ta => ConvertTypeSignature(ta, sharedTypeParams))
                    .ToList()
            };
        }

        // Handle non-generic CLR types (like RangeIterator)
        // Try to resolve the CLR type from the stored ClrTypeName
        if (!string.IsNullOrEmpty(signature.ClrTypeName))
        {
            // Try direct Type.GetType first
            var clrType = Type.GetType(signature.ClrTypeName);

            // If that fails, search loaded assemblies
            // Note: This could be optimized by caching assembly lookups if performance becomes an issue
            if (clrType == null)
            {
                clrType ??= AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(signature.ClrTypeName))
                    .FirstOrDefault(t => t != null);
            }

            if (clrType != null)
            {
                // Types with [SharpyModuleType] (e.g., ArgumentParser, Path) are imported as
                // UserDefinedType in the symbol table, so operator return types must also be
                // UserDefinedType for assignability to work.
                // Uses pre-computed set from discovery phase instead of runtime reflection.
                if (clrType.FullName != null && _moduleTypeNames.ContainsKey(clrType.FullName))
                {
                    return new UserDefinedType { Name = signature.Name };
                }

                return new BuiltinType
                {
                    Name = signature.Name,
                    ClrType = clrType
                };
            }
        }

        // Fallback
        return SemanticType.Object;
    }
}
