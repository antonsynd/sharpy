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
    // Cache of TypeSymbol instances keyed by CLR full type name, so ConvertTypeSignature
    // can reuse the same TypeSymbol instance created by ConvertToTypeSymbol.
    private readonly ConcurrentDictionary<string, TypeSymbol> _moduleTypeSymbols = new();
    // Shared TypeParameterType instances for skeleton TypeSymbols, keyed by CLR full type
    // name. All methods on a generic type must share these instances so that reference
    // equality holds during generic substitution.
    private readonly ConcurrentDictionary<string, TypeParameterType[]> _skeletonTypeParams = new();

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

        // Pre-compute module type names and TypeSymbols for ConvertTypeSignature.
        // TypeSymbols must be created here (before GetModuleFunctions) so that
        // ConvertTypeSignature can reuse the same TypeSymbol instances, ensuring
        // reference identity for type assignability checks.
        //
        // Three-pass approach required because types may cross-reference each
        // other (e.g., ArgumentParser.parse_args() returns Namespace):
        //   Pass 1: Register all fullNames in _moduleTypeNames so
        //           ConvertTypeSignature recognizes them as UserDefinedType.
        //   Pass 2: Create skeleton TypeSymbols (identity only) and register in
        //           _moduleTypeSymbols so cross-references get a stable instance.
        //   Pass 3: Populate members on each skeleton. Member resolution may
        //           call ConvertTypeSignature, which now finds the skeleton.
        var pendingTypeInfos = new List<(string fullName, string moduleName, Caching.DiscoveredTypeInfo typeInfo)>();
        foreach (var (moduleName, moduleOverloads) in index.Modules)
        {
            foreach (var typeInfo in moduleOverloads.Types)
            {
                if (typeInfo.IsModuleType)
                {
                    var fullName = typeInfo.Namespace + "." + typeInfo.Name;
                    _moduleTypeNames.TryAdd(fullName, 0);
                    pendingTypeInfos.Add((fullName, moduleName, typeInfo));
                }
            }
        }

        foreach (var (fullName, moduleName, typeInfo) in pendingTypeInfos)
        {
            if (!_moduleTypeSymbols.ContainsKey(fullName))
            {
                var skeleton = CreateSkeletonTypeSymbol(typeInfo, DefiningModuleFor(moduleName));
                if (skeleton != null)
                {
                    _moduleTypeSymbols.TryAdd(fullName, skeleton);

                    // Create shared TypeParameterType instances for generic skeletons.
                    // All methods on this type will reference these same instances,
                    // ensuring reference equality during generic substitution.
                    if (skeleton.IsGeneric)
                    {
                        var sharedParams = skeleton.TypeParameters
                            .Select(tp => new TypeParameterType
                            {
                                Name = tp.Name,
                                DeclaringType = skeleton
                            })
                            .ToArray();
                        _skeletonTypeParams.TryAdd(fullName, sharedParams);
                    }
                }
            }
        }

        foreach (var (fullName, _, typeInfo) in pendingTypeInfos)
        {
            if (_moduleTypeSymbols.TryGetValue(fullName, out var skeleton))
            {
                // Use stored shared type params so all methods share the same
                // TypeParameterType instances for this generic type.
                _skeletonTypeParams.TryGetValue(fullName, out var storedParams);
                PopulateTypeSymbolMembers(skeleton, typeInfo, sharedTypeParams: storedParams);
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
                // Reuse pre-computed TypeSymbol if available (for reference identity)
                var fullName = typeInfo.Namespace + "." + typeInfo.Name;
                if (_moduleTypeSymbols.TryGetValue(fullName, out var cached))
                {
                    types.Add(cached);
                }
                else
                {
                    var typeSymbol = ConvertToTypeSymbol(typeInfo, definingModule: DefiningModuleFor(moduleName));
                    if (typeSymbol != null)
                        types.Add(typeSymbol);
                }
            }
        }

        return types;
    }

    /// <summary>
    /// Convert a cached DiscoveredTypeInfo to a TypeSymbol.
    /// </summary>
    private TypeSymbol? ConvertToTypeSymbol(Caching.DiscoveredTypeInfo typeInfo, string? definingModule = null)
    {
        return ConvertToTypeSymbol(typeInfo, sharedTypeParams: null, definingModule);
    }

    /// <summary>
    /// Convert a cached DiscoveredTypeInfo to a TypeSymbol, optionally remapping generic type
    /// parameters to shared instances. When <paramref name="definingModule"/> is supplied, it is
    /// recorded on the symbol so the emitter fully-qualifies module-exported types.
    /// </summary>
    private TypeSymbol? ConvertToTypeSymbol(
        Caching.DiscoveredTypeInfo typeInfo, TypeParameterType[]? sharedTypeParams, string? definingModule = null)
    {
        var skeleton = CreateSkeletonTypeSymbol(typeInfo, definingModule);
        if (skeleton == null)
            return null;
        PopulateTypeSymbolMembers(skeleton, typeInfo, sharedTypeParams);
        return skeleton;
    }

    /// <summary>
    /// Maps a discovery module name to the value recorded as <see cref="Symbol.DefiningModule"/>.
    /// Returns null for the synthetic "builtins" module so globally-available types (ValueError,
    /// Complex, etc.) keep emitting by their simple name; named stdlib modules (fractions,
    /// datetime, ...) get their module recorded so the emitter fully-qualifies module-exported
    /// references (e.g. <c>x: fractions.Fraction</c>).
    /// </summary>
    private static string? DefiningModuleFor(string moduleName)
        => string.IsNullOrEmpty(moduleName) || moduleName == "builtins" ? null : moduleName;

    /// <summary>
    /// Create a TypeSymbol with only identity fields (Name, Kind, ClrType) populated.
    /// Member lists remain empty so that cross-type references resolved during
    /// <see cref="PopulateTypeSymbolMembers"/> can find a stable TypeSymbol instance
    /// via <see cref="_moduleTypeSymbols"/>.
    /// For generic CLR types, TypeParameters are populated so that
    /// <see cref="TypeSymbol.IsGeneric"/> returns true.
    /// When <paramref name="definingModule"/> is supplied (module-exported types), it is recorded
    /// on the symbol so the emitter fully-qualifies references via its CLR type name; this matches
    /// the from-import re-export path (CreateReExportedTypeSymbol), which also sets DefiningModule.
    /// </summary>
    private TypeSymbol? CreateSkeletonTypeSymbol(Caching.DiscoveredTypeInfo typeInfo, string? definingModule = null)
    {
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

        // Extract generic type parameters from the CLR type definition
        var typeParams = new List<Parser.Ast.TypeParameterDef>();
        if (clrType is { IsGenericTypeDefinition: true })
        {
            var clrArgs = clrType.GetGenericArguments();
            for (int i = 0; i < clrArgs.Length; i++)
            {
                typeParams.Add(new Parser.Ast.TypeParameterDef
                {
                    Name = $"T{i}",
                    Variance = ClrTypeMapper.GetClrVariance(clrArgs[i])
                });
            }
        }

        return new TypeSymbol
        {
            Name = typeInfo.Name,
            Kind = SymbolKind.Type,
            TypeKind = typeKind,
            ClrType = clrType,
            TypeParameters = typeParams,
            AccessLevel = AccessLevel.Public,
            IsAbstract = clrType?.IsAbstract == true && !clrType.IsInterface,
            Documentation = typeInfo.Documentation,
            DefiningModule = definingModule
        };
    }

    /// <summary>
    /// Populate members (methods, operators, protocols, properties) into an existing
    /// TypeSymbol. Cross-type references resolved via ConvertTypeSignature will reuse
    /// TypeSymbol instances that were already registered in <see cref="_moduleTypeSymbols"/>.
    /// </summary>
    private void PopulateTypeSymbolMembers(
        TypeSymbol typeSymbol,
        Caching.DiscoveredTypeInfo typeInfo,
        TypeParameterType[]? sharedTypeParams)
    {
        // Convert methods from discovery, expanding default parameters into separate overloads
        if (typeInfo.Methods.Count > 0)
        {
            var expanderTypeName = ClrNameHelper.StripArity(typeInfo.Name);

            foreach (var sig in typeInfo.Methods)
            {
                var expanded = OverloadExpander.Expand(sig, expanderTypeName);
                foreach (var overloadSig in expanded)
                {
                    typeSymbol.Methods.Add(
                        ConvertToFunctionSymbol(overloadSig, typeInfo.Name, sharedTypeParams));
                }
            }
        }

        // Convert operator methods from discovery with full signatures preserved.
        // TypeInferenceService.FindBestOverload needs parameter types and return types
        // to resolve operator overloads (e.g., Path / str -> Path).
        foreach (var kvp in ConvertOperatorMethods(typeInfo.OperatorMethods, sharedTypeParams))
        {
            typeSymbol.OperatorMethods[kvp.Key] = kvp.Value;
        }

        // Convert protocol methods from discovery as marker-only stubs (same rationale).
        foreach (var kvp in NormalizeToDunderStubs(typeInfo.ProtocolMethods))
        {
            typeSymbol.ProtocolMethods[kvp.Key] = kvp.Value;
        }

        // Convert properties from discovery
        foreach (var p in typeInfo.Properties)
        {
            typeSymbol.Properties.Add(new PropertySymbol
            {
                Name = ReverseNameMangler.ToSharpyName(p.Name, ReverseNameContext.Property),
                Type = ConvertTypeSignature(p.PropertyType, sharedTypeParams),
                HasGetter = p.HasGetter,
                HasSetter = p.HasSetter,
                Documentation = p.Documentation
            });
        }

        // Populate MethodOverloads using the canonical helper (filters dunders, groups by name).
        foreach (var kvp in TypeSymbol.BuildMethodOverloads(typeSymbol.Methods))
        {
            typeSymbol.MethodOverloads[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Explicit mapping from Sharpy type names to CLR type names in the discovery index.
    /// Eliminates case-insensitive matching by making the naming contract explicit.
    /// Only types whose Sharpy name differs from the CLR name need entries here.
    /// </summary>
    private static readonly Dictionary<string, string> SharpyToClrNameMap = new()
    {
        ["bytes"] = "Bytes",
        ["list"] = "List",
        ["dict"] = "Dict",
        ["set"] = "Set",
        ["str"] = "Str",
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
                    var name = ClrNameHelper.StripArity(t.Name);
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
    /// Get all field symbols from a specific module.
    /// Returns tuples of (Name, SemanticType, IsConst) for each field.
    /// </summary>
    public List<(string Name, SemanticType Type, bool IsConst)> GetModuleFields(string moduleName)
    {
        var fields = new List<(string Name, SemanticType Type, bool IsConst)>();

        foreach (var lazy in _loadedIndices.Values)
        {
            var index = lazy.Value;
            if (!index.Modules.TryGetValue(moduleName, out var moduleOverloads))
                continue;

            foreach (var (fieldName, fieldSignature) in moduleOverloads.Fields)
            {
                var semanticType = ConvertTypeSignature(fieldSignature.FieldType);
                fields.Add((fieldName, semanticType, fieldSignature.IsConst));
            }
        }

        return fields;
    }

    /// <summary>
    /// Get the C# namespace of the [SharpyModule] class for a module, or null if not available.
    /// </summary>
    public string? GetModuleCSharpNamespace(string moduleName)
    {
        foreach (var lazy in _loadedIndices.Values)
        {
            var index = lazy.Value;
            if (index.Modules.TryGetValue(moduleName, out var moduleOverloads)
                && moduleOverloads.CSharpNamespace != null)
            {
                return moduleOverloads.CSharpNamespace;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the simple C# class name of the [SharpyModule] class for a module
    /// (e.g. "EmailModule" for "email"), or null if not available.
    /// </summary>
    public string? GetModuleCSharpClassName(string moduleName)
    {
        foreach (var lazy in _loadedIndices.Values)
        {
            var index = lazy.Value;
            if (index.Modules.TryGetValue(moduleName, out var moduleOverloads)
                && moduleOverloads.CSharpClassName != null)
            {
                return moduleOverloads.CSharpClassName;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the XML documentation summary for a module, or null if not available.
    /// </summary>
    public string? GetModuleDocumentation(string moduleName)
    {
        foreach (var lazy in _loadedIndices.Values)
        {
            var index = lazy.Value;
            if (index.Modules.TryGetValue(moduleName, out var moduleOverloads)
                && moduleOverloads.Documentation != null)
            {
                return moduleOverloads.Documentation;
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
    /// Get the assembly identity name that contains the given module,
    /// or null if not found.
    /// </summary>
    public string? GetAssemblyNameForModule(string moduleName)
    {
        foreach (var (assemblyName, lazy) in _loadedIndices)
        {
            var index = lazy.Value;
            if (index.Modules.ContainsKey(moduleName))
                return assemblyName;
        }
        return null;
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
                    IsVariadic = p.IsVariadic,
                    Documentation = p.Documentation,
                    ClrTypeName = string.IsNullOrEmpty(p.Type.ClrTypeName) ? null : p.Type.ClrTypeName
                })
                .ToList(),
            AccessLevel = AccessLevel.Public,
            TypeParameters = signature.TypeParameters.Count > 0
                ? signature.TypeParameters
                    .Select(name => new Parser.Ast.TypeParameterDef { Name = name })
                    .ToList()
                : new List<Parser.Ast.TypeParameterDef>(),
            IsVirtual = signature.IsVirtual,
            IsAbstract = signature.IsAbstract,
            Documentation = signature.Documentation,
            ClrMethodName = signature.ClrName ?? ExtractClrNameFromMethodToken(signature.MethodToken)
        };
    }

    /// <summary>
    /// Extracts the original CLR method name from a method token of the form
    /// "Assembly|Type|MethodName|ParamCount". Returns null if the token is empty
    /// or malformed. Used as a fallback for cache entries created before ClrName
    /// was stored directly on the signature.
    /// </summary>
    private static string? ExtractClrNameFromMethodToken(string? methodToken)
    {
        if (string.IsNullOrEmpty(methodToken))
            return null;
        var parts = methodToken!.Split('|');
        return parts.Length >= 3 ? parts[2] : null;
    }

    /// <summary>
    /// Extract the element type from a variadic parameter's type signature.
    /// Variadic parameters are stored as list[T] but we need just T for type checking.
    /// </summary>
    private SemanticType GetVariadicElementType(
        TypeSignature typeSignature, TypeParameterType[]? sharedTypeParams = null)
    {
        // Variadic parameters are stored as array[T] (mapped from T[])
        // We need to extract T
        if (typeSignature.IsGeneric && (typeSignature.Name.StartsWith("array") || typeSignature.Name.StartsWith("list")) && typeSignature.TypeArguments.Count == 1)
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
            // Method-level type params (e.g., U in Map<U>) must NOT be remapped
            // to the declaring type's shared params — they are distinct parameters.
            if (signature.IsMethodLevelTypeParam)
            {
                return new TypeParameterType { Name = signature.Name };
            }

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

            // Handle value-type Nullable<T> (C# T?) -> NullableType (#890)
            if (signature.Name == Caching.TypeSignature.NullableSentinel && signature.TypeArguments.Count == 1)
            {
                return new NullableType
                {
                    UnderlyingType = ConvertTypeSignature(signature.TypeArguments[0], sharedTypeParams)
                };
            }

            // Handle __func__[T1, ..., TResult] -> FunctionType (from Func<...> CLR types)
            if (signature.Name == TypeSignature.FuncSentinel && signature.TypeArguments.Count >= 1)
            {
                var allArgs = signature.TypeArguments
                    .Select(ta => ConvertTypeSignature(ta, sharedTypeParams))
                    .ToList();
                return new FunctionType
                {
                    ParameterTypes = allArgs.Take(allArgs.Count - 1).ToList(),
                    ReturnType = allArgs[allArgs.Count - 1]
                };
            }

            // Handle __action__[T1, ...] -> FunctionType with void return
            if (signature.Name == TypeSignature.ActionSentinel && signature.TypeArguments.Count >= 1)
            {
                var allArgs = signature.TypeArguments
                    .Select(ta => ConvertTypeSignature(ta, sharedTypeParams))
                    .ToList();
                return new FunctionType
                {
                    ParameterTypes = allArgs,
                    ReturnType = SemanticType.Void
                };
            }

            // Handle Result[T, E] -> ResultType
            if (signature.Name == BuiltinNames.Result && signature.TypeArguments.Count == 2)
            {
                return new ResultType
                {
                    OkType = ConvertTypeSignature(signature.TypeArguments[0], sharedTypeParams),
                    ErrorType = ConvertTypeSignature(signature.TypeArguments[1], sharedTypeParams)
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
                    // Reuse the pre-computed TypeSymbol (created during index loading)
                    // so the Symbol reference is identical to the one returned by
                    // GetModuleTypes, enabling type assignability checks.
                    if (_moduleTypeSymbols.TryGetValue(clrType.FullName, out var cachedSymbol))
                    {
                        return new UserDefinedType { Name = signature.Name, Symbol = cachedSymbol };
                    }
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
