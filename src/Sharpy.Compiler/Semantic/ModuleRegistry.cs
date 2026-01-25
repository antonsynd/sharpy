using System.Collections.Concurrent;
using System.Reflection;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Registry for managing third-party .NET modules.
/// Handles loading and discovery of functions from external assemblies.
/// Thread-safe for concurrent use.
/// </summary>
public class ModuleRegistry
{
    private readonly CachedModuleDiscovery _discovery;
    private readonly ICompilerLogger _logger;
    private readonly ConcurrentDictionary<string, Assembly> _loadedAssemblies = new();
    private readonly ConcurrentBag<string> _modulePaths = new();
    private readonly ConcurrentBag<SemanticError> _errors = new();

    public ModuleRegistry(ICompilerLogger? logger = null, OverloadIndexCache? cache = null)
    {
        _discovery = new CachedModuleDiscovery(cache);
        _logger = logger ?? NullLogger.Instance;
    }

    public IReadOnlyList<SemanticError> Errors => _errors.ToList();

    /// <summary>
    /// Get all configured module search paths.
    /// </summary>
    public IEnumerable<string> GetModulePaths() => _modulePaths.ToList();

    /// <summary>
    /// Add a path to search for module assemblies.
    /// </summary>
    public void AddModulePath(string path)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogWarning($"Module path does not exist: {path}", 0, 0);
            return;
        }

        // Note: ConcurrentBag allows duplicates, but this is acceptable as
        // ResolveAssemblyPath will find the assembly on first match anyway
        _modulePaths.Add(path);
        _logger.LogDebug($"Added module search path: {path}");
    }

    /// <summary>
    /// Load a .NET assembly as a module reference.
    /// Discovers all public static methods in classes named "Exports".
    /// </summary>
    public bool LoadReference(string assemblyPath)
    {
        try
        {
            // Resolve the assembly path
            var resolvedPath = ResolveAssemblyPath(assemblyPath);
            if (resolvedPath == null)
            {
                AddError($"Assembly not found: {assemblyPath}");
                return false;
            }

            // Load the assembly
            var assembly = Assembly.LoadFrom(resolvedPath);
            var assemblyName = assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(resolvedPath);

            // Use TryAdd for atomic check-and-add operation
            if (!_loadedAssemblies.TryAdd(assemblyName, assembly))
            {
                _logger.LogDebug($"Assembly '{assemblyName}' already loaded");
                return true;
            }

            // Discover functions from the assembly
            _discovery.LoadAssembly(assembly);

            _logger.LogInfo($"Loaded module reference: {assemblyName} from {resolvedPath}");
            return true;
        }
        catch (IOException ex)
        {
            AddError($"Failed to load assembly '{assemblyPath}': {ex.Message}");
            return false;
        }
        catch (BadImageFormatException ex)
        {
            AddError($"Invalid assembly format '{assemblyPath}': {ex.Message}");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            AddError($"Access denied loading assembly '{assemblyPath}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get all functions exported by a specific module.
    /// Module name should match the assembly/namespace name.
    /// </summary>
    public List<FunctionSymbol> GetModuleFunctions(string moduleName)
    {
        try
        {
            return _discovery.GetModuleFunctions(moduleName);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning($"Module '{moduleName}' not found: {ex.Message}", 0, 0);
            return new List<FunctionSymbol>();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Error getting functions for module '{moduleName}': {ex.Message}", 0, 0);
            return new List<FunctionSymbol>();
        }
    }

    /// <summary>
    /// Get all loaded module names.
    /// </summary>
    public IEnumerable<string> GetLoadedModules()
    {
        return _discovery.GetLoadedModules();
    }

    /// <summary>
    /// Check if a module with the given name is loaded.
    /// </summary>
    public bool IsModuleLoaded(string moduleName)
    {
        return GetLoadedModules().Any(m =>
            m.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clear the overload discovery cache.
    /// </summary>
    public void ClearCache()
    {
        _discovery.ClearCache();
        _logger.LogInfo("Cleared module discovery cache");
    }

    /// <summary>
    /// Try to resolve a .NET type from a module/type name.
    /// Maps Sharpy module names to .NET namespaces:
    /// - "system" -> "System"
    /// - "system.collections" -> "System.Collections"
    /// - "system.io" -> "System.IO"
    /// </summary>
    /// <param name="moduleName">The Sharpy module name (e.g., "system")</param>
    /// <param name="typeName">The type name to import (e.g., "Exception")</param>
    /// <returns>The resolved .NET Type, or null if not found</returns>
    public Type? TryResolveNetType(string moduleName, string typeName)
    {
        // Map Sharpy module name to .NET namespace
        var netNamespace = MapModuleToNamespace(moduleName);
        if (netNamespace == null)
        {
            _logger.LogDebug($"Module '{moduleName}' is not a known .NET namespace");
            return null;
        }

        // Construct the full .NET type name
        var fullTypeName = $"{netNamespace}.{typeName}";

        // Try to find the type in loaded assemblies and the runtime
        var clrType = Type.GetType(fullTypeName);
        if (clrType != null)
        {
            _logger.LogDebug($"Resolved .NET type: {fullTypeName}");
            return clrType;
        }

        // Try searching in all loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            clrType = assembly.GetType(fullTypeName);
            if (clrType != null)
            {
                _logger.LogDebug($"Resolved .NET type from assembly {assembly.GetName().Name}: {fullTypeName}");
                return clrType;
            }
        }

        _logger.LogDebug($".NET type not found: {fullTypeName}");
        return null;
    }

    /// <summary>
    /// Check if a module name maps to a .NET namespace.
    /// </summary>
    public bool IsNetNamespace(string moduleName)
    {
        return MapModuleToNamespace(moduleName) != null;
    }

    /// <summary>
    /// Get all public types from a .NET namespace.
    /// </summary>
    /// <param name="moduleName">The Sharpy module name (e.g., "system")</param>
    /// <returns>List of TypeSymbols for all public types in the namespace</returns>
    public List<TypeSymbol> GetNamespaceTypes(string moduleName)
    {
        var types = new List<TypeSymbol>();
        var netNamespace = MapModuleToNamespace(moduleName);
        if (netNamespace == null)
            return types;

        // Search all loaded assemblies for types in this namespace
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var namespaceTypes = assembly.GetTypes()
                    .Where(t => t.IsPublic && t.Namespace == netNamespace);

                foreach (var clrType in namespaceTypes)
                {
                    var typeSymbol = CreateTypeSymbolFromClrType(clrType);
                    if (typeSymbol != null)
                    {
                        types.Add(typeSymbol);
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Skip assemblies that can't be fully loaded
            }
        }

        return types;
    }

    /// <summary>
    /// Create a TypeSymbol from a CLR type.
    /// </summary>
    public TypeSymbol? CreateTypeSymbolFromClrType(Type clrType)
    {
        if (clrType == null)
            return null;

        var typeKind = clrType.IsInterface ? TypeKind.Interface
                     : clrType.IsEnum ? TypeKind.Enum
                     : clrType.IsValueType ? TypeKind.Struct
                     : TypeKind.Class;

        var typeSymbol = new TypeSymbol
        {
            Name = clrType.Name,
            Kind = SymbolKind.Type,
            TypeKind = typeKind,
            ClrType = clrType,
            IsAbstract = clrType.IsAbstract && !clrType.IsInterface,
            AccessLevel = AccessLevel.Public
        };

        // Set base type for classes (except System.Object)
        if (clrType.BaseType != null && clrType.BaseType != typeof(object))
        {
            var baseTypeSymbol = CreateTypeSymbolFromClrType(clrType.BaseType);
            if (baseTypeSymbol != null)
            {
                // Set the BaseType property directly on the TypeSymbol
                typeSymbol.BaseType = baseTypeSymbol;
            }
        }

        // Add implemented interfaces
        foreach (var iface in clrType.GetInterfaces())
        {
            // Only add directly implemented interfaces (not inherited ones)
            if (clrType.BaseType != null && clrType.BaseType.GetInterfaces().Contains(iface))
                continue;

            var ifaceSymbol = CreateTypeSymbolFromClrType(iface);
            if (ifaceSymbol != null)
            {
                typeSymbol.Interfaces.Add(ifaceSymbol);
            }
        }

        // Add constructors (as __init__ methods for Sharpy compatibility)
        var constructors = clrType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var ctor in constructors)
        {
            var ctorSymbol = CreateConstructorSymbol(ctor, clrType);
            if (ctorSymbol != null)
            {
                typeSymbol.Constructors.Add(ctorSymbol);
            }
        }

        return typeSymbol;
    }

    /// <summary>
    /// Create a FunctionSymbol for a .NET constructor, mapped as __init__.
    ///
    /// Note: We DO include 'self' as the first parameter to match Sharpy conventions.
    /// The type checker uses .Skip(1) when building FunctionType from constructors,
    /// so we need the 'self' parameter for the skip to work correctly.
    /// </summary>
    private FunctionSymbol? CreateConstructorSymbol(System.Reflection.ConstructorInfo ctor, Type declaringType)
    {
        var typeMapper = new Discovery.TypeMapper();
        var parameters = new List<ParameterSymbol>();

        // Add 'self' parameter first (Sharpy convention - will be skipped by type checker)
        parameters.Add(new ParameterSymbol
        {
            Name = "self",
            Type = new UserDefinedType { Name = declaringType.Name }
        });

        // Add constructor parameters
        foreach (var param in ctor.GetParameters())
        {
            var paramType = typeMapper.MapClrTypeToSemanticType(param.ParameterType);
            parameters.Add(new ParameterSymbol
            {
                Name = param.Name ?? $"arg{param.Position}",
                Type = paramType,
                HasDefault = param.HasDefaultValue
            });
        }

        return new FunctionSymbol
        {
            Name = "__init__",
            Kind = SymbolKind.Function,
            ReturnType = SemanticType.Void,
            Parameters = parameters,
            AccessLevel = AccessLevel.Public,
            ClrMethod = null  // ConstructorInfo isn't a MethodInfo, leave null
        };
    }

    /// <summary>
    /// Map a Sharpy module name to a .NET namespace.
    /// Uses lowercase Python-style names (e.g., "system" -> "System").
    /// </summary>
    private string? MapModuleToNamespace(string moduleName)
    {
        // Standard .NET namespace mappings
        return moduleName.ToLowerInvariant() switch
        {
            "system" => "System",
            "system.collections" => "System.Collections",
            "system.collections.generic" => "System.Collections.Generic",
            "system.io" => "System.IO",
            "system.text" => "System.Text",
            "system.linq" => "System.Linq",
            "system.threading" => "System.Threading",
            "system.threading.tasks" => "System.Threading.Tasks",
            "system.net" => "System.Net",
            "system.net.http" => "System.Net.Http",
            _ => null
        };
    }

    /// <summary>
    /// Resolve an assembly path by checking:
    /// 1. Absolute path if file exists
    /// 2. Relative to current directory
    /// 3. Module search paths
    /// Note: TOCTOU (Time-of-Check-Time-of-Use) race condition is acceptable here
    /// as any file system changes between this check and Assembly.LoadFrom() are
    /// handled by exception catching (IOException, UnauthorizedAccessException) in LoadReference().
    /// </summary>
    private string? ResolveAssemblyPath(string assemblyPath)
    {
        // Try as absolute or relative path first
        if (File.Exists(assemblyPath))
        {
            return Path.GetFullPath(assemblyPath);
        }

        // Try in current directory
        var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), assemblyPath);
        if (File.Exists(currentDirPath))
        {
            return Path.GetFullPath(currentDirPath);
        }

        // Try in module search paths
        foreach (var searchPath in _modulePaths)
        {
            var fullPath = Path.Combine(searchPath, assemblyPath);
            if (File.Exists(fullPath))
            {
                return Path.GetFullPath(fullPath);
            }

            // Also try with .dll extension if not present
            if (!assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var dllPath = Path.Combine(searchPath, assemblyPath + ".dll");
                if (File.Exists(dllPath))
                {
                    return Path.GetFullPath(dllPath);
                }
            }
        }

        return null;
    }

    private void AddError(string message)
    {
        _errors.Add(new SemanticError(message, null, null));
    }
}
