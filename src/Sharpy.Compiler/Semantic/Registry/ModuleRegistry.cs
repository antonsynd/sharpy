using System.Collections.Concurrent;
using System.Reflection;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Registry;

/// <summary>
/// Registry for managing third-party .NET modules.
/// Handles loading and discovery of functions from external assemblies.
/// Thread-safe for concurrent use.
/// </summary>
internal class ModuleRegistry
{
    private readonly CachedModuleDiscovery _discovery;
    private readonly ICompilerLogger _logger;
    private readonly ConcurrentDictionary<string, Assembly> _loadedAssemblies = new();
    private readonly ConcurrentBag<string> _modulePaths = new();
    private readonly DiagnosticBag _diagnostics = new();
    private readonly ConcurrentDictionary<string, string> _assemblyNameToPath = new();
    private readonly ConcurrentDictionary<string, byte> _usedAssemblyPaths = new();

    public ModuleRegistry(ICompilerLogger? logger = null, OverloadIndexCache? cache = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _discovery = new CachedModuleDiscovery(cache, _logger);
    }

    public DiagnosticBag Diagnostics => _diagnostics;

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
    /// Discovers all public static methods in [SharpyModule]-decorated classes.
    /// </summary>
    public bool LoadReference(string assemblyPath)
    {
        try
        {
            // Resolve the assembly path
            var resolvedPath = ResolveAssemblyPath(assemblyPath);
            if (resolvedPath == null)
            {
                _diagnostics.AddPhaseError($"Assembly not found: {assemblyPath}", CompilerPhase.ImportResolution, code: DiagnosticCodes.Semantic.AssemblyNotFound);
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

            _assemblyNameToPath.TryAdd(assemblyName, resolvedPath);

            _logger.LogInfo($"Loaded module reference: {assemblyName} from {resolvedPath}");
            return true;
        }
        catch (IOException ex)
        {
            _diagnostics.AddPhaseError($"Failed to load assembly '{assemblyPath}': {ex.Message}", CompilerPhase.ImportResolution, code: DiagnosticCodes.Semantic.AssemblyLoadError);
            return false;
        }
        catch (BadImageFormatException ex)
        {
            _diagnostics.AddPhaseError($"Invalid assembly format '{assemblyPath}': {ex.Message}", CompilerPhase.ImportResolution, code: DiagnosticCodes.Semantic.AssemblyLoadError);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _diagnostics.AddPhaseError($"Access denied loading assembly '{assemblyPath}': {ex.Message}", CompilerPhase.ImportResolution, code: DiagnosticCodes.Semantic.AssemblyLoadError);
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
            var result = _discovery.GetModuleFunctions(moduleName);
            if (result.Count > 0)
                RecordModuleUsage(moduleName);
            return result;
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
    /// Get types exported by a module (e.g., ArgumentParser from argparse).
    /// </summary>
    public List<TypeSymbol> GetModuleTypes(string moduleName)
    {
        try
        {
            var result = _discovery.GetModuleTypes(moduleName);
            if (result.Count > 0)
                RecordModuleUsage(moduleName);
            return result;
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning($"Module '{moduleName}' not found: {ex.Message}", 0, 0);
            return new List<TypeSymbol>();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Error getting types for module '{moduleName}': {ex.Message}", 0, 0);
            return new List<TypeSymbol>();
        }
    }

    /// <summary>
    /// Get fields exported by a module (e.g., string constants from the string module).
    /// Returns tuples of (Name, SemanticType, IsConst) for each field.
    /// </summary>
    public List<(string Name, SemanticType Type, bool IsConst)> GetModuleFields(string moduleName)
    {
        try
        {
            var result = _discovery.GetModuleFields(moduleName);
            if (result.Count > 0)
                RecordModuleUsage(moduleName);
            return result;
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning($"Module '{moduleName}' not found: {ex.Message}", 0, 0);
            return new List<(string Name, SemanticType Type, bool IsConst)>();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Error getting fields for module '{moduleName}': {ex.Message}", 0, 0);
            return new List<(string Name, SemanticType Type, bool IsConst)>();
        }
    }

    /// <summary>
    /// Get the C# namespace of the [SharpyModule] class for a module, or null if not available.
    /// </summary>
    public string? GetModuleCSharpNamespace(string moduleName)
    {
        return _discovery.GetModuleCSharpNamespace(moduleName);
    }

    /// <summary>
    /// Get the XML documentation summary for a module, or null if not available.
    /// </summary>
    public string? GetModuleDocumentation(string moduleName)
    {
        return _discovery.GetModuleDocumentation(moduleName);
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
    /// Get the file paths of all assemblies that contributed modules actually
    /// used during compilation. Sharpy.Core is always included.
    /// </summary>
    public IReadOnlySet<string> GetUsedAssemblyPaths()
    {
        return new HashSet<string>(_usedAssemblyPaths.Keys, StringComparer.OrdinalIgnoreCase);
    }

    private void RecordModuleUsage(string moduleName)
    {
        var assemblyName = _discovery.GetAssemblyNameForModule(moduleName);
        if (assemblyName != null && _assemblyNameToPath.TryGetValue(assemblyName, out var path))
        {
            _usedAssemblyPaths.TryAdd(path, 0);
        }
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
    /// Get the .NET namespace name for a Sharpy module name (e.g., "system" -> "System").
    /// Returns null if the module name doesn't map to a .NET namespace.
    /// </summary>
    public string? GetNetNamespace(string moduleName)
    {
        return MapModuleToNamespace(moduleName);
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
            catch (ReflectionTypeLoadException ex)
            {
                // Skip assemblies that can't be fully loaded
                _logger.LogDebug($"Skipping assembly '{assembly.GetName().Name}': {ex.Message}");
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

        var typeName = ClrNameHelper.StripArity(clrType.Name);

        // Build TypeParameterDefs from CLR generic arguments
        var typeParameters = new List<TypeParameterDef>();
        if (clrType.IsGenericTypeDefinition)
        {
            foreach (var arg in clrType.GetGenericArguments())
            {
                typeParameters.Add(new TypeParameterDef { Name = arg.Name });
            }
        }

        // Collect all data before construction so TypeSymbol properties can be init-only

        // Resolve base type for classes (except System.Object)
        TypeSymbol? baseTypeSymbol = null;
        if (clrType.BaseType != null && clrType.BaseType != typeof(object))
        {
            baseTypeSymbol = CreateTypeSymbolFromClrType(clrType.BaseType);
        }

        // Collect implemented interfaces (only directly implemented, not inherited)
        var interfaces = new List<InterfaceReference>();
        foreach (var iface in clrType.GetInterfaces())
        {
            if (clrType.BaseType != null && clrType.BaseType.GetInterfaces().Contains(iface))
                continue;

            var ifaceSymbol = CreateTypeSymbolFromClrType(iface);
            if (ifaceSymbol != null)
            {
                interfaces.Add(new InterfaceReference { Definition = ifaceSymbol });
            }
        }

        // Collect constructors (as __init__ methods for Sharpy compatibility)
        var ctorSymbols = new List<FunctionSymbol>();
        var constructors = clrType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var ctor in constructors)
        {
            var ctorSymbol = CreateConstructorSymbol(ctor, clrType);
            if (ctorSymbol != null)
            {
                ctorSymbols.Add(ctorSymbol);
            }
        }

        var typeSymbol = new TypeSymbol
        {
            Name = typeName,
            Kind = SymbolKind.Type,
            TypeKind = typeKind,
            ClrType = clrType,
            IsAbstract = clrType.IsAbstract && !clrType.IsInterface,
            AccessLevel = AccessLevel.Public,
            BaseType = baseTypeSymbol,
            Interfaces = interfaces,
            Constructors = ctorSymbols,
            TypeParameters = typeParameters
        };

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
        var typeMapper = new Discovery.ClrTypeMapper();
        var parameters = new List<ParameterSymbol>();

        // Add 'self' parameter first (Sharpy convention - will be skipped by type checker)
        parameters.Add(new ParameterSymbol
        {
            Name = PythonNames.Self,
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
            Name = DunderNames.Init,
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
            "system.text.regular_expressions" => "System.Text.RegularExpressions",
            "system.linq" => "System.Linq",
            "system.threading" => "System.Threading",
            "system.threading.tasks" => "System.Threading.Tasks",
            "system.net" => "System.Net",
            "system.net.http" => "System.Net.Http",
            "system.runtime.interop_services" => "System.Runtime.InteropServices",
            "sharpy" => "Sharpy",
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

}
