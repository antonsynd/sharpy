using System.Reflection;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Registry for managing third-party .NET modules.
/// Handles loading and discovery of functions from external assemblies.
/// </summary>
public class ModuleRegistry
{
    private readonly CachedModuleDiscovery _discovery;
    private readonly ICompilerLogger _logger;
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new();
    private readonly List<string> _modulePaths = new();
    private readonly List<SemanticError> _errors = new();

    public ModuleRegistry(ICompilerLogger? logger = null)
    {
        _discovery = new CachedModuleDiscovery();
        _logger = logger ?? NullLogger.Instance;
    }

    public IReadOnlyList<SemanticError> Errors => _errors;

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

        if (!_modulePaths.Contains(path))
        {
            _modulePaths.Add(path);
            _logger.LogDebug($"Added module search path: {path}");
        }
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

            // Check if already loaded
            if (_loadedAssemblies.ContainsKey(assemblyName))
            {
                _logger.LogDebug($"Assembly '{assemblyName}' already loaded");
                return true;
            }

            // Discover functions from the assembly
            _discovery.LoadAssembly(assembly);
            _loadedAssemblies[assemblyName] = assembly;

            _logger.LogInfo($"Loaded module reference: {assemblyName} from {resolvedPath}");
            return true;
        }
        catch (Exception ex)
        {
            AddError($"Failed to load assembly '{assemblyPath}': {ex.Message}");
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
        catch (Exception ex)
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
    /// Resolve an assembly path by checking:
    /// 1. Absolute path if file exists
    /// 2. Relative to current directory
    /// 3. Module search paths
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
