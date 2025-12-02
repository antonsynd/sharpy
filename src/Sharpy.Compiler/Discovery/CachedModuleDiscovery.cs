using System.Collections.Concurrent;
using System.Reflection;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Discovers and caches function overloads from assemblies.
/// Uses reflection on first load, then caches results for subsequent loads.
/// Thread-safe for concurrent use.
/// </summary>
public class CachedModuleDiscovery
{
    private readonly OverloadIndexCache _cache;
    private readonly OverloadIndexBuilder _builder;
    private readonly TypeMapper _typeMapper;
    private readonly ConcurrentDictionary<string, OverloadIndex> _loadedIndices = new();

    /// <summary>
    /// Create a discovery instance using the default cache directory.
    /// </summary>
    public CachedModuleDiscovery() : this(null)
    {
    }

    /// <summary>
    /// Create a discovery instance with a custom cache.
    /// </summary>
    /// <param name="cache">Custom cache instance. If null, creates a default cache.</param>
    public CachedModuleDiscovery(OverloadIndexCache? cache)
    {
        _cache = cache ?? new OverloadIndexCache();
        _builder = new OverloadIndexBuilder();
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
                    Type = ConvertTypeSignature(p.Type),
                    HasDefault = p.HasDefault,
                    // Note: DefaultValue Expression reconstruction is simplified
                    DefaultValue = null  // TODO: Reconstruct from cached string
                })
                .ToList(),
            AccessLevel = AccessLevel.Public
        };
    }

    /// <summary>
    /// Convert a TypeSignature back to a SemanticType.
    /// </summary>
    private SemanticType ConvertTypeSignature(TypeSignature signature)
    {
        // Handle primitive types
        if (signature.Name == "int") return SemanticType.Int;
        if (signature.Name == "long") return SemanticType.Long;
        if (signature.Name == "float") return SemanticType.Float;
        if (signature.Name == "double") return SemanticType.Double;
        if (signature.Name == "bool") return SemanticType.Bool;
        if (signature.Name == "str") return SemanticType.Str;
        if (signature.Name == "None") return SemanticType.Void;
        if (signature.Name == "object") return SemanticType.Object;

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
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    clrType = assembly.GetType(signature.ClrTypeName);
                    if (clrType != null) break;
                }
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
