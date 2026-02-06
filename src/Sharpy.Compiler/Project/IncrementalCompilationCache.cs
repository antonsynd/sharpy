using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Project;

/// <summary>
/// Metadata wrapper for the hash cache, including compiler version for cache invalidation.
/// </summary>
internal record CacheMetadata(string CompilerVersion, Dictionary<string, string> FileHashes);

/// <summary>
/// Versioned envelope for the symbol cache to handle schema evolution.
/// </summary>
/// <remarks>
/// Schema version history:
///   v1 (2026-02): Initial versioned format
///
/// When making breaking changes:
///   1. Increment CurrentSchemaVersion
///   2. Add migration logic if data can be upgraded
///   3. Document the change here
/// </remarks>
internal record SymbolCacheEnvelope(int SchemaVersion, Dictionary<string, FileCacheEntry> Files);

/// <summary>
/// Manages file content hashes and symbol caches for incremental compilation.
/// Persists hashes and compiled artifacts to disk between builds to enable skipping unchanged files.
/// </summary>
internal class IncrementalCompilationCache
{
    /// <summary>
    /// Current schema version for the symbol cache.
    /// Increment this when making breaking changes to FileCacheEntry or CachedSymbol structures.
    /// </summary>
    internal const int CurrentSchemaVersion = 2;

    private readonly string _cacheFilePath;
    private readonly string _symbolCachePath;
    private readonly ICompilerLogger _logger;
    private Dictionary<string, string> _fileHashes;
    private Dictionary<string, FileCacheEntry>? _fileCache;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets the number of files that are stale (need recompilation).
    /// </summary>
    public int StaleFileCount { get; private set; }

    /// <summary>
    /// Gets the number of files that are up-to-date (skipped).
    /// </summary>
    public int UpToDateFileCount { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IncrementalCompilationCache"/> class.
    /// </summary>
    /// <param name="projectConfig">The project configuration.</param>
    /// <param name="logger">Optional logger for debug output.</param>
    public IncrementalCompilationCache(ProjectConfig projectConfig, ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;

        // Store cache in obj/{Configuration}/.sharpy-cache
        var objDir = Path.Combine(projectConfig.ProjectDirectory, "obj", projectConfig.Configuration);
        Directory.CreateDirectory(objDir);
        _cacheFilePath = Path.Combine(objDir, ".sharpy-cache");
        _symbolCachePath = Path.Combine(objDir, ".sharpy-symbols");

        _fileHashes = LoadHashCache();
    }

    /// <summary>
    /// Computes the SHA-256 hash of a file's contents.
    /// </summary>
    public static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hashBytes = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hashBytes);
    }

    /// <summary>
    /// Checks if a file has changed since the last build.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>True if the file is stale (changed or new), false if unchanged.</returns>
    public bool IsStale(string filePath)
    {
        if (!File.Exists(filePath))
            return true;

        var normalizedPath = PathNormalizer.Normalize(filePath);
        var currentHash = ComputeFileHash(filePath);

        if (!_fileHashes.TryGetValue(normalizedPath, out var cachedHash))
        {
            // File not in cache (new file)
            return true;
        }

        return !string.Equals(cachedHash, currentHash, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines which files need to be recompiled based on content hashes
    /// and dependency relationships.
    /// </summary>
    /// <param name="allFiles">All source files in the project.</param>
    /// <param name="dependencyGraph">The dependency graph, if available.</param>
    /// <returns>A set of files that need recompilation.</returns>
    public HashSet<string> GetFilesToRecompile(IEnumerable<string> allFiles, DependencyGraph? dependencyGraph)
    {
        var filesToRecompile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changedFiles = new List<string>();

        // First pass: find directly changed files
        foreach (var file in allFiles)
        {
            if (IsStale(file))
            {
                changedFiles.Add(file);
                filesToRecompile.Add(file);
            }
        }

        // Second pass: include transitively affected files
        if (dependencyGraph != null && changedFiles.Count > 0)
        {
            var affected = dependencyGraph.GetAffectedFiles(changedFiles);
            foreach (var affectedFile in affected)
            {
                filesToRecompile.Add(affectedFile);
            }
        }

        StaleFileCount = filesToRecompile.Count;
        UpToDateFileCount = allFiles.Count() - StaleFileCount;

        if (_logger.IsEnabled(CompilerLogLevel.Debug))
        {
            _logger.LogDebug($"Incremental: {StaleFileCount} stale, {UpToDateFileCount} up-to-date");
            foreach (var file in changedFiles)
            {
                _logger.LogDebug($"  Changed: {Path.GetFileName(file)}");
            }
        }

        return filesToRecompile;
    }

    /// <summary>
    /// Updates the hash for a successfully compiled file.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    public void UpdateHash(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var normalizedPath = PathNormalizer.Normalize(filePath);
        var hash = ComputeFileHash(filePath);
        _fileHashes[normalizedPath] = hash;
    }

    /// <summary>
    /// Saves the hash cache to disk.
    /// </summary>
    public void SaveCache()
    {
        try
        {
            var metadata = new CacheMetadata(GetCompilerVersion(), _fileHashes);
            var json = JsonSerializer.Serialize(metadata, s_jsonOptions);

            var directory = Path.GetDirectoryName(_cacheFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_cacheFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to save incremental cache: {ex.Message}", 0, 0);
        }
    }

    /// <summary>
    /// Clears the cache, forcing a full rebuild on the next compilation.
    /// </summary>
    public void Clear()
    {
        _fileHashes.Clear();
        _fileCache?.Clear();

        DeleteFileIfExists(_cacheFilePath);
        DeleteFileIfExists(_symbolCachePath);
    }

    #region File Cache (Symbols and Generated Code)

    /// <summary>
    /// Saves the file cache entry for a successfully compiled file.
    /// </summary>
    /// <param name="filePath">The path to the source file.</param>
    /// <param name="symbols">The symbols declared in this file.</param>
    /// <param name="generatedCSharp">The generated C# code.</param>
    /// <param name="dependencies">The file paths this file depends on (imports).</param>
    /// <param name="modulePath">Optional module path for this file.</param>
    public void SaveFileCache(
        string filePath,
        List<Symbol> symbols,
        string generatedCSharp,
        List<string> dependencies,
        string? modulePath = null)
    {
        EnsureFileCacheLoaded();

        var normalizedPath = PathNormalizer.Normalize(filePath);
        var contentHash = File.Exists(filePath) ? ComputeFileHash(filePath) : string.Empty;

        var cachedSymbols = symbols
            .Select(s => SymbolSerializer.Serialize(s, filePath))
            .ToList();

        var entry = new FileCacheEntry
        {
            ContentHash = contentHash,
            Symbols = cachedSymbols,
            GeneratedCSharp = generatedCSharp,
            Dependencies = dependencies.Select(PathNormalizer.Normalize).ToList(),
            ModulePath = modulePath
        };

        _fileCache![normalizedPath] = entry;
    }

    /// <summary>
    /// Gets the cached file entry for a source file.
    /// </summary>
    /// <param name="filePath">The path to the source file.</param>
    /// <returns>The cached entry, or null if not found or stale.</returns>
    public FileCacheEntry? GetFileCache(string filePath)
    {
        EnsureFileCacheLoaded();

        var normalizedPath = PathNormalizer.Normalize(filePath);
        if (!_fileCache!.TryGetValue(normalizedPath, out var entry))
        {
            return null;
        }

        // Verify the entry is still valid by checking the content hash
        if (!File.Exists(filePath))
        {
            return null;
        }

        var currentHash = ComputeFileHash(filePath);
        if (!string.Equals(entry.ContentHash, currentHash, StringComparison.Ordinal))
        {
            // File has changed since cache was created
            return null;
        }

        return entry;
    }

    /// <summary>
    /// Checks if a file has valid cached data available.
    /// </summary>
    /// <param name="filePath">The path to the source file.</param>
    /// <returns>True if valid cache exists, false otherwise.</returns>
    public bool HasValidFileCache(string filePath)
    {
        return GetFileCache(filePath) != null;
    }

    /// <summary>
    /// Loads all caches from disk (hash cache and symbol cache).
    /// </summary>
    public void LoadAllCaches()
    {
        _fileHashes = LoadHashCache();
        _fileCache = LoadSymbolCache();
    }

    /// <summary>
    /// Builds a dependency graph from cached file dependencies.
    /// This allows determining transitive affected files before parsing.
    /// </summary>
    /// <param name="allFiles">All source files in the project.</param>
    /// <returns>A dependency graph built from cached dependencies, or null if no cache exists.</returns>
    public DependencyGraph? BuildCachedDependencyGraph(IEnumerable<string> allFiles)
    {
        EnsureFileCacheLoaded();

        if (_fileCache == null || _fileCache.Count == 0)
        {
            return null;
        }

        var builder = new DependencyGraphBuilder();

        // Add all files first
        foreach (var file in allFiles)
        {
            builder.AddFile(file);
        }

        // Add cached dependency edges
        foreach (var file in allFiles)
        {
            var normalizedPath = PathNormalizer.Normalize(file);
            if (_fileCache.TryGetValue(normalizedPath, out var entry))
            {
                foreach (var dep in entry.Dependencies)
                {
                    // Only add dependencies to files that exist in the project
                    builder.AddDependency(file, dep);
                }
            }
        }

        return builder.Build();
    }

    /// <summary>
    /// Saves all caches to disk (hash cache and symbol cache).
    /// </summary>
    public void SaveAllCaches()
    {
        SaveCache(); // Hash cache

        if (_fileCache != null && _fileCache.Count > 0)
        {
            SaveSymbolCache();
        }
    }

    /// <summary>
    /// Restores symbols from the file cache into the symbol registry.
    /// </summary>
    /// <param name="filePath">The path to the source file.</param>
    /// <param name="symbolRegistry">The registry to populate with restored symbols.</param>
    /// <returns>True if symbols were restored, false if no valid cache.</returns>
    public bool RestoreSymbols(string filePath, Dictionary<string, Symbol> symbolRegistry)
    {
        var entry = GetFileCache(filePath);
        if (entry == null)
        {
            return false;
        }

        foreach (var cachedSymbol in entry.Symbols)
        {
            var symbol = SymbolSerializer.Deserialize(cachedSymbol, symbolRegistry);
            symbolRegistry[cachedSymbol.Id] = symbol;
        }

        // Resolve cross-references
        SymbolSerializer.ResolveReferences(entry.Symbols, symbolRegistry);

        if (_logger.IsEnabled(CompilerLogLevel.Debug))
        {
            _logger.LogDebug($"Restored {entry.Symbols.Count} symbols from cache for {Path.GetFileName(filePath)}");
        }

        return true;
    }

    #endregion

    #region Private Methods

    private void EnsureFileCacheLoaded()
    {
        _fileCache ??= LoadSymbolCache();
    }

    private Dictionary<string, string> LoadHashCache()
    {
        if (!File.Exists(_cacheFilePath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(_cacheFilePath);

            // Try to deserialize as new CacheMetadata format (with compiler version)
            var metadata = JsonSerializer.Deserialize<CacheMetadata>(json, s_jsonOptions);
            if (metadata != null)
            {
                var currentVersion = GetCompilerVersion();
                if (metadata.CompilerVersion != currentVersion)
                {
                    _logger.LogInfo($"Compiler version changed ({metadata.CompilerVersion} -> {currentVersion}); invalidating cache");
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                // Convert to case-insensitive dictionary
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in metadata.FileHashes)
                {
                    result[kvp.Key] = kvp.Value;
                }
                return result;
            }

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            // Try to load as legacy format (plain dictionary without version)
            try
            {
                var json = File.ReadAllText(_cacheFilePath);
                var legacyCache = JsonSerializer.Deserialize<Dictionary<string, string>>(json, s_jsonOptions);
                if (legacyCache != null)
                {
                    _logger.LogInfo("Legacy cache format detected; invalidating to upgrade");
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // Ignore nested exception
            }
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to load incremental cache, starting fresh: {ex.Message}", 0, 0);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private Dictionary<string, FileCacheEntry> LoadSymbolCache()
    {
        if (!File.Exists(_symbolCachePath))
        {
            return new Dictionary<string, FileCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(_symbolCachePath);

            // Try to deserialize as new versioned envelope format
            var envelope = JsonSerializer.Deserialize<SymbolCacheEnvelope>(json, s_jsonOptions);
            if (envelope != null)
            {
                if (envelope.SchemaVersion != CurrentSchemaVersion)
                {
                    _logger.LogInfo($"Symbol cache schema version {envelope.SchemaVersion} != {CurrentSchemaVersion}; rebuilding");
                    return new Dictionary<string, FileCacheEntry>(StringComparer.OrdinalIgnoreCase);
                }

                // Convert to case-insensitive dictionary
                var result = new Dictionary<string, FileCacheEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in envelope.Files)
                {
                    result[kvp.Key] = kvp.Value;
                }
                return result;
            }

            return new Dictionary<string, FileCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            // Try to load as legacy format (plain dictionary without version)
            try
            {
                var legacyJson = File.ReadAllText(_symbolCachePath);
                var legacyCache = JsonSerializer.Deserialize<Dictionary<string, FileCacheEntry>>(legacyJson, s_jsonOptions);
                if (legacyCache != null)
                {
                    _logger.LogInfo("Legacy symbol cache format detected; rebuilding");
                    return new Dictionary<string, FileCacheEntry>(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // Ignore nested exception
            }
            return new Dictionary<string, FileCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to load symbol cache, starting fresh: {ex.Message}", 0, 0);
            return new Dictionary<string, FileCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveSymbolCache()
    {
        if (_fileCache == null)
        {
            return;
        }

        try
        {
            var envelope = new SymbolCacheEnvelope(CurrentSchemaVersion, _fileCache);
            var json = JsonSerializer.Serialize(envelope, s_jsonOptions);

            var directory = Path.GetDirectoryName(_symbolCachePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_symbolCachePath, json);

            if (_logger.IsEnabled(CompilerLogLevel.Debug))
            {
                _logger.LogDebug($"Saved symbol cache v{CurrentSchemaVersion} with {_fileCache.Count} entries");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to save symbol cache: {ex.Message}", 0, 0);
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Ignore deletion failures
            }
        }
    }

    /// <summary>
    /// Gets the current compiler version string for cache invalidation.
    /// Includes assembly version and a hash of the assembly content for development builds.
    /// </summary>
    internal static string GetCompilerVersion()
    {
        var assembly = typeof(IncrementalCompilationCache).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0";

        // Include assembly content hash for debug builds where version doesn't change
        // This ensures cache invalidation during development
        var assemblyPath = assembly.Location;
        if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
        {
            try
            {
                var bytes = File.ReadAllBytes(assemblyPath);
                var hash = Convert.ToHexStringLower(SHA256.HashData(bytes)[..8]);
                return $"{version}-{hash}";
            }
            catch
            {
                // If we can't read the assembly, just use the version
            }
        }

        return version;
    }

    #endregion
}
