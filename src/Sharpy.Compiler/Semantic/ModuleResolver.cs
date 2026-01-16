using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Resolves module paths to actual source files (.spy files).
/// Supports multiple search paths and package directory resolution.
/// </summary>
public class ModuleResolver
{
    private readonly ICompilerLogger _logger;
    private readonly List<string> _searchPaths = new();
    private string? _currentModulePath = null;

    public ModuleResolver(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public ModuleResolver(ICompilerLogger? logger, IEnumerable<string>? searchPaths) : this(logger)
    {
        if (searchPaths != null)
        {
            _searchPaths.AddRange(searchPaths);
        }
    }

    /// <summary>
    /// Configure the current module's directory for resolving relative imports
    /// </summary>
    public void SetCurrentModulePath(string modulePath)
    {
        _currentModulePath = modulePath;
    }

    /// <summary>
    /// Add a search path (for project directories, stdlib, packages)
    /// </summary>
    public void AddSearchPath(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            _searchPaths.Add(path);
        }
    }

    /// <summary>
    /// Resolve a module name to a file path
    /// </summary>
    /// <param name="moduleName">The dotted module name (e.g., "utils.helpers")</param>
    /// <returns>Resolution result if found, null otherwise</returns>
    public ModuleResolutionResult? Resolve(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            _logger.LogDebug("Cannot resolve empty module name");
            return null;
        }

        // Convert module.submodule to module/submodule.spy
        var relativePath = moduleName.Replace('.', Path.DirectorySeparatorChar) + ".spy";
        var packagePath = moduleName.Replace('.', Path.DirectorySeparatorChar);

        var searchedPaths = new List<string>();

        // 1. Try relative to current module (if set)
        if (_currentModulePath != null)
        {
            var currentDir = Path.GetDirectoryName(_currentModulePath);
            if (currentDir != null)
            {
                // Try module file
                var result = TryResolveInDirectory(
                    currentDir,
                    relativePath,
                    packagePath,
                    moduleName,
                    ModuleResolutionKind.RelativeToCurrentModule,
                    searchedPaths,
                    currentDir);

                if (result != null)
                    return result;
            }
        }

        // 2. Try project search paths
        foreach (var searchPath in _searchPaths)
        {
            var result = TryResolveInDirectory(
                searchPath,
                relativePath,
                packagePath,
                moduleName,
                ModuleResolutionKind.ProjectSearchPath,
                searchedPaths,
                searchPath);

            if (result != null)
                return result;
        }

        // 3. Try current working directory (fallback)
        var cwd = Directory.GetCurrentDirectory();
        var cwdResult = TryResolveInDirectory(
            cwd,
            relativePath,
            packagePath,
            moduleName,
            ModuleResolutionKind.CurrentWorkingDirectory,
            searchedPaths,
            null);

        if (cwdResult != null)
            return cwdResult;

        // Log where we looked
        _logger.LogDebug($"Module '{moduleName}' not found. Searched: {string.Join(", ", searchedPaths)}");

        return null;
    }

    /// <summary>
    /// Try to resolve a module in a specific directory
    /// </summary>
    private ModuleResolutionResult? TryResolveInDirectory(
        string baseDir,
        string relativePath,
        string packagePath,
        string moduleName,
        ModuleResolutionKind kind,
        List<string> searchedPaths,
        string? searchPath)
    {
        // Try as a direct file: module.spy
        var filePath = Path.Combine(baseDir, relativePath);
        searchedPaths.Add(filePath);

        if (File.Exists(filePath))
        {
            _logger.LogDebug($"Resolved module '{moduleName}' to {filePath} ({kind})");
            return new ModuleResolutionResult
            {
                FullPath = Path.GetFullPath(filePath),
                ModuleName = moduleName,
                Kind = kind,
                SearchPath = searchPath
            };
        }

        // Try as a package directory: module/__init__.spy
        var packageDir = Path.Combine(baseDir, packagePath);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        searchedPaths.Add(initPath);

        if (File.Exists(initPath))
        {
            _logger.LogDebug($"Resolved module '{moduleName}' to package {initPath} ({kind})");
            return new ModuleResolutionResult
            {
                FullPath = Path.GetFullPath(initPath),
                ModuleName = moduleName,
                Kind = kind,
                SearchPath = searchPath
            };
        }

        return null;
    }
}

/// <summary>
/// Result of module resolution
/// </summary>
public class ModuleResolutionResult
{
    /// <summary>
    /// Absolute path to the resolved .spy file
    /// </summary>
    public string FullPath { get; init; } = string.Empty;

    /// <summary>
    /// Original module name that was resolved
    /// </summary>
    public string ModuleName { get; init; } = string.Empty;

    /// <summary>
    /// How the module was resolved (which search path or strategy)
    /// </summary>
    public ModuleResolutionKind Kind { get; init; }

    /// <summary>
    /// The search path that matched (if applicable)
    /// </summary>
    public string? SearchPath { get; init; }
}

/// <summary>
/// Indicates how a module was resolved
/// </summary>
public enum ModuleResolutionKind
{
    /// <summary>
    /// Found relative to the importing file's directory
    /// </summary>
    RelativeToCurrentModule,

    /// <summary>
    /// Found in one of the project's configured search paths
    /// </summary>
    ProjectSearchPath,

    /// <summary>
    /// Found in the standard library (future feature)
    /// </summary>
    StandardLibrary,

    /// <summary>
    /// Found in an external package (future feature)
    /// </summary>
    ExternalPackage,

    /// <summary>
    /// Found in the current working directory (fallback)
    /// </summary>
    CurrentWorkingDirectory
}
