using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Project;

/// <summary>
/// Manages file content hashes for incremental compilation.
/// Persists hashes to disk between builds to enable skipping unchanged files.
/// </summary>
internal class IncrementalCompilationCache
{
    private readonly string _cacheFilePath;
    private readonly ICompilerLogger _logger;
    private Dictionary<string, string> _fileHashes;

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

        _fileHashes = LoadCache();
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

        var normalizedPath = NormalizePath(filePath);
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

        var normalizedPath = NormalizePath(filePath);
        var hash = ComputeFileHash(filePath);
        _fileHashes[normalizedPath] = hash;
    }

    /// <summary>
    /// Saves the cache to disk.
    /// </summary>
    public void SaveCache()
    {
        try
        {
            var json = JsonSerializer.Serialize(_fileHashes, new JsonSerializerOptions { WriteIndented = true });
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
        if (File.Exists(_cacheFilePath))
        {
            try
            {
                File.Delete(_cacheFilePath);
            }
            catch
            {
                // Ignore deletion failures
            }
        }
    }

    private Dictionary<string, string> LoadCache()
    {
        if (!File.Exists(_cacheFilePath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to load incremental cache, starting fresh: {ex.Message}", 0, 0);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string NormalizePath(string path)
    {
        var normalized = Path.GetFullPath(path).Replace('\\', '/');
        if (!OperatingSystem.IsLinux())
        {
            normalized = normalized.ToLowerInvariant();
        }
        return normalized;
    }
}
