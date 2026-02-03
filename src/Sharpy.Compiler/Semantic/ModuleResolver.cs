using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Resolves module paths to actual source files (.spy files).
/// Supports multiple search paths and package directory resolution.
/// </summary>
internal class ModuleResolver
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
    /// <param name="moduleName">The dotted module name (e.g., "utils.helpers") or relative import (e.g., ".helpers", "..parent")</param>
    /// <returns>Resolution result if found, null otherwise</returns>
    public ModuleResolutionResult? Resolve(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            _logger.LogDebug("Cannot resolve empty module name");
            return null;
        }

        var searchedPaths = new List<string>();

        // Handle relative imports (starting with .)
        if (moduleName.StartsWith("."))
        {
            return ResolveRelativeImport(moduleName, searchedPaths);
        }

        // Convert module.submodule to module/submodule.spy
        var relativePath = moduleName.Replace('.', Path.DirectorySeparatorChar) + ".spy";
        var packagePath = moduleName.Replace('.', Path.DirectorySeparatorChar);

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
    /// Resolve a relative import (starting with . or ..)
    /// </summary>
    /// <param name="moduleName">The relative module name (e.g., ".helpers", "..parent", "...")</param>
    /// <param name="searchedPaths">List to track searched paths for error reporting</param>
    /// <returns>Resolution result if found, null otherwise</returns>
    private ModuleResolutionResult? ResolveRelativeImport(string moduleName, List<string> searchedPaths)
    {
        if (_currentModulePath == null)
        {
            _logger.LogDebug($"Cannot resolve relative import '{moduleName}' without current module context");
            return null;
        }

        var currentDir = Path.GetDirectoryName(_currentModulePath);
        if (currentDir == null)
        {
            _logger.LogDebug($"Cannot get directory for current module: {_currentModulePath}");
            return null;
        }

        // Count leading dots to determine how many parent directories to go up
        int dotCount = 0;
        while (dotCount < moduleName.Length && moduleName[dotCount] == '.')
        {
            dotCount++;
        }

        // Get the module part after the leading dots
        var moduleNamePart = dotCount < moduleName.Length ? moduleName.Substring(dotCount) : "";

        // For a single dot, start from current package directory
        // For each additional dot, go up one directory level
        var baseDir = currentDir;
        for (int i = 1; i < dotCount; i++)
        {
            var parentDir = Path.GetDirectoryName(baseDir);
            if (parentDir == null)
            {
                _logger.LogDebug($"Cannot go up {dotCount - 1} levels from {currentDir}");
                return null;
            }
            baseDir = parentDir;
        }

        // If there's no module name part (e.g., just ".."), we're importing the parent package itself
        if (string.IsNullOrEmpty(moduleNamePart))
        {
            // Look for __init__.spy in the target directory
            var initPath = Path.Combine(baseDir, "__init__.spy");
            searchedPaths.Add(initPath);

            if (File.Exists(initPath))
            {
                _logger.LogDebug($"Resolved relative import '{moduleName}' to package {initPath}");
                var canonicalName = ComputeCanonicalModuleName(Path.GetFullPath(initPath));
                return new ModuleResolutionResult
                {
                    FullPath = Path.GetFullPath(initPath),
                    ModuleName = moduleName,
                    CanonicalModuleName = canonicalName,
                    Kind = ModuleResolutionKind.RelativeToCurrentModule,
                    SearchPath = currentDir
                };
            }
        }
        else
        {
            // Convert remaining module path (e.g., "helpers.utils" -> "helpers/utils")
            var relativePath = moduleNamePart.Replace('.', Path.DirectorySeparatorChar);

            // Try as a direct file: module.spy
            var filePath = Path.Combine(baseDir, relativePath + ".spy");
            searchedPaths.Add(filePath);

            if (File.Exists(filePath))
            {
                _logger.LogDebug($"Resolved relative import '{moduleName}' to {filePath}");
                var canonicalName = ComputeCanonicalModuleName(Path.GetFullPath(filePath));
                return new ModuleResolutionResult
                {
                    FullPath = Path.GetFullPath(filePath),
                    ModuleName = moduleName,
                    CanonicalModuleName = canonicalName,
                    Kind = ModuleResolutionKind.RelativeToCurrentModule,
                    SearchPath = currentDir
                };
            }

            // Try as a package directory: module/__init__.spy
            var packageDir = Path.Combine(baseDir, relativePath);
            var initPath = Path.Combine(packageDir, "__init__.spy");
            searchedPaths.Add(initPath);

            if (File.Exists(initPath))
            {
                _logger.LogDebug($"Resolved relative import '{moduleName}' to package {initPath}");
                var canonicalName = ComputeCanonicalModuleName(Path.GetFullPath(initPath));
                return new ModuleResolutionResult
                {
                    FullPath = Path.GetFullPath(initPath),
                    ModuleName = moduleName,
                    CanonicalModuleName = canonicalName,
                    Kind = ModuleResolutionKind.RelativeToCurrentModule,
                    SearchPath = currentDir
                };
            }
        }

        _logger.LogDebug($"Module '{moduleName}' not found. Searched: {string.Join(", ", searchedPaths)}");
        return null;
    }

    /// <summary>
    /// Compute the canonical (fully-qualified) module name from a file path.
    /// For relative imports, computes the package path based on directory structure.
    /// </summary>
    private string? ComputeCanonicalModuleName(string fullPath)
    {
        // Normalize the path
        fullPath = Path.GetFullPath(fullPath);
        var fullPathDir = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileNameWithoutExtension(fullPath);

        if (fullPathDir == null)
            return fileName;

        // For relative imports from a package __init__.spy, we need to build
        // the canonical path using the package directory structure.
        // Walk up the directory tree, collecting package names until we find a non-package directory.
        var packageParts = new List<string>();
        var currentDir = fullPathDir;

        // Start with the target module's directory
        while (currentDir != null)
        {
            var dirName = Path.GetFileName(currentDir);
            var initFile = Path.Combine(currentDir, "__init__.spy");

            // Check if this directory is a package (has __init__.spy)
            if (File.Exists(initFile))
            {
                packageParts.Insert(0, dirName);
                currentDir = Path.GetDirectoryName(currentDir);
            }
            else
            {
                // We've found the source root (non-package directory)
                break;
            }
        }

        // Build the canonical name
        if (fileName != DunderNames.Init)
        {
            packageParts.Add(fileName);
        }

        // If no packages were found, just return the filename
        if (packageParts.Count == 0)
        {
            return fileName;
        }

        return string.Join(".", packageParts);
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
internal class ModuleResolutionResult
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
    /// The canonical (fully-qualified) module name for relative imports.
    /// For example, when ".helpers" is resolved from "mypackage/__init__.spy",
    /// this would be "mypackage.helpers".
    /// </summary>
    public string? CanonicalModuleName { get; init; }

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
internal enum ModuleResolutionKind
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
