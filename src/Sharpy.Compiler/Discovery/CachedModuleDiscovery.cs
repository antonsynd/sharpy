using System.Collections.Concurrent;
using System.Reflection;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Discovers and caches function overloads from assemblies.
/// Uses reflection on first load, then caches results for subsequent loads.
/// Thread-safe for concurrent use.
/// </summary>
internal class CachedModuleDiscovery
{
    private readonly OverloadIndexCache _cache;
    private readonly OverloadIndexBuilder _builder;
    private readonly TypeMapper _typeMapper;
    private readonly ConcurrentDictionary<string, OverloadIndex> _loadedIndices = new();

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
        _typeMapper = new TypeMapper();
    }

    /// <summary>
    /// Load an assembly and discover its functions.
    /// Uses cache if available, otherwise builds and caches.
    /// </summary>
    public void LoadAssembly(Assembly assembly)
    {
        var identity = AssemblyIdentity.FromAssembly(assembly);

        // Use GetOrAdd for thread-safe loading
        _loadedIndices.GetOrAdd(identity.Name, _ =>
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
        });
    }

    /// <summary>
    /// Get all function symbols from a specific module.
    /// </summary>
    public List<FunctionSymbol> GetModuleFunctions(string moduleName)
    {
        var functions = new List<FunctionSymbol>();

        foreach (var index in _loadedIndices.Values)
        {
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

        foreach (var index in _loadedIndices.Values)
        {
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

        return new TypeSymbol
        {
            Name = typeInfo.Name,
            Kind = SymbolKind.Type,
            TypeKind = typeKind,
            ClrType = clrType,
            AccessLevel = AccessLevel.Public,
            IsAbstract = clrType?.IsAbstract == true && !clrType.IsInterface
        };
    }

    /// <summary>
    /// Get all loaded modules.
    /// </summary>
    public IEnumerable<string> GetLoadedModules()
    {
        return _loadedIndices.Values
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
    /// Convert a cached FunctionSignature back to a FunctionSymbol.
    /// </summary>
    private FunctionSymbol ConvertToFunctionSymbol(FunctionSignature signature, string moduleName)
    {
        return new FunctionSymbol
        {
            Name = signature.Name,
            Kind = SymbolKind.Function,
            ReturnType = ConvertTypeSignature(signature.ReturnType),
            Parameters = signature.Parameters
                .Select(p => new ParameterSymbol
                {
                    Name = p.Name,
                    // For variadic parameters, extract the element type from list[T] -> T
                    Type = p.IsVariadic ? GetVariadicElementType(p.Type) : ConvertTypeSignature(p.Type),
                    HasDefault = p.HasDefault,
                    DefaultValue = p.HasDefault ? DefaultValueParser.Parse(p.DefaultValue) : null,
                    IsVariadic = p.IsVariadic
                })
                .ToList(),
            AccessLevel = AccessLevel.Public
        };
    }

    /// <summary>
    /// Extract the element type from a variadic parameter's type signature.
    /// Variadic parameters are stored as list[T] but we need just T for type checking.
    /// </summary>
    private SemanticType GetVariadicElementType(TypeSignature typeSignature)
    {
        // Variadic parameters are stored as list[T] (mapped from T[])
        // We need to extract T
        if (typeSignature.IsGeneric && typeSignature.Name.StartsWith("list") && typeSignature.TypeArguments.Count == 1)
        {
            return ConvertTypeSignature(typeSignature.TypeArguments[0]);
        }
        // Fallback to the full type if not a list
        return ConvertTypeSignature(typeSignature);
    }

    /// <summary>
    /// Convert a TypeSignature back to a SemanticType.
    /// </summary>
    private SemanticType ConvertTypeSignature(TypeSignature signature)
    {
        // Handle primitive types
        if (signature.Name == "int")
            return SemanticType.Int;
        if (signature.Name == "long")
            return SemanticType.Long;
        if (signature.Name == "float")
            return SemanticType.Float;       // float -> double (per spec)
        if (signature.Name == "float32")
            return SemanticType.Float32;   // float32 -> C# float
        if (signature.Name == "float64")
            return SemanticType.Double;    // float64 -> double
        if (signature.Name == "double")
            return SemanticType.Double;
        if (signature.Name == "bool")
            return SemanticType.Bool;
        if (signature.Name == "str")
            return SemanticType.Str;
        if (signature.Name == "None")
            return SemanticType.Void;
        if (signature.Name == "object")
            return SemanticType.Object;

        // Handle generic types
        if (signature.IsGeneric)
        {
            return new GenericType
            {
                // Extract base name before '[' if present; otherwise use the whole name.
                Name = signature.Name.Contains('[')
                    ? signature.Name[..signature.Name.IndexOf('[')]
                    : signature.Name,
                TypeArguments = signature.TypeArguments
                    .Select(ConvertTypeSignature)
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
