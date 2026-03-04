using System.IO.Compression;
using System.Text.Json;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Discovery.Caching;

/// <summary>
/// Manages persistent caching of overload indexes.
/// Cache location: ~/.sharpy/cache/overload-index/ (or custom directory if specified)
/// Thread-safe for concurrent access from multiple processes.
/// </summary>
internal class OverloadIndexCache
{
    private readonly string _cacheDirectory;
    private readonly ICompilerLogger _logger;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 100;
    private const int CurrentCacheFormatVersion = 6;

    // Using camelCase for JSON serialization to reduce file size and follow common conventions.
    // DefaultIgnoreCondition.WhenWritingNull reduces cache file size by omitting null properties.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Create a cache using the default cache directory (~/.sharpy/cache/overload-index/).
    /// </summary>
    public OverloadIndexCache() : this(null, null)
    {
    }

    /// <summary>
    /// Create a cache using a custom cache directory.
    /// </summary>
    /// <param name="cacheDirectory">
    /// Custom cache directory path. If null, uses the default location.
    /// Useful for tests to avoid conflicts between parallel test runs.
    /// </param>
    /// <param name="logger">Optional logger. If null, uses NullLogger.</param>
    public OverloadIndexCache(string? cacheDirectory, ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;

        if (cacheDirectory != null)
        {
            _cacheDirectory = cacheDirectory;
        }
        else
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _cacheDirectory = Path.Combine(userHome, ".sharpy", "cache", "overload-index");
        }

        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    /// <summary>
    /// Try to load a cached index for the given assembly identity.
    /// Retries on file access conflicts from parallel processes.
    /// </summary>
    public OverloadIndex? TryLoad(AssemblyIdentity identity)
    {
        var cacheKey = identity.ToCacheKey();
        var cachePath = Path.Combine(_cacheDirectory, cacheKey);

        if (!File.Exists(cachePath))
            return null;

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var fileStream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                var index = JsonSerializer.Deserialize<OverloadIndex>(gzipStream, JsonOptions);

                // Reject caches from older format versions
                if (index?.CacheFormatVersion != CurrentCacheFormatVersion)
                {
                    TryDeleteFile(cachePath);
                    return null;
                }

                // Verify the identity matches
                if (index.Identity.Equals(identity))
                {
                    return index;
                }

                // Cache is stale, delete it
                TryDeleteFile(cachePath);
                return null;
            }
            catch (IOException) when (attempt < MaxRetries - 1)
            {
                // File is locked by another process, wait and retry
                Thread.Sleep(RetryDelayMs * (attempt + 1));
            }
            catch (Exception ex)
            {
                // Cache file is corrupted, incompatible, or inaccessible
                _logger.LogDebug($"Failed to load cache from '{cachePath}': {ex.GetType().Name} - {ex.Message}");
                TryDeleteFile(cachePath);
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Save an index to the cache.
    /// Uses atomic write (write to temp file, then rename) to prevent corruption.
    /// Retries on file access conflicts from parallel processes.
    /// </summary>
    public void Save(OverloadIndex index)
    {
        var cacheKey = index.Identity.ToCacheKey();
        var cachePath = Path.Combine(_cacheDirectory, cacheKey);
        var tempPath = cachePath + $".{Guid.NewGuid():N}.tmp";

        // Clean up old cache files for this assembly (different versions/hashes older than 7 days)
        CleanupOldCaches(index.Identity.Name, cacheKey);

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // Write to a temporary file first
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
                {
                    JsonSerializer.Serialize(gzipStream, index, JsonOptions);
                }

                // Atomically move the temp file to the final location
                // File.Move with overwrite handles the case where another process wrote the file
                File.Move(tempPath, cachePath, overwrite: true);
                return; // Success
            }
            catch (IOException) when (attempt < MaxRetries - 1)
            {
                // File is locked by another process, wait and retry
                Thread.Sleep(RetryDelayMs * (attempt + 1));
            }
            catch (Exception ex)
            {
                // Clean up temp file on error
                TryDeleteFile(tempPath);

                // Log but don't fail - caching is optional
                _logger.LogDebug($"Failed to save cache (attempt {attempt + 1}/{MaxRetries}): {ex.Message}");

                if (attempt >= MaxRetries - 1)
                {
                    // Only warn user on final failure, and make the message less alarming
                    // since cache failures are non-critical
                    _logger.LogWarning($"Cache save failed after {MaxRetries} attempts: {ex.Message}", 0, 0);
                }
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Clear all cached indexes.
    /// </summary>
    public void ClearAll()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            foreach (var file in Directory.GetFiles(_cacheDirectory, "*.json.gz"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to delete cache file '{file}': {ex.Message}", 0, 0);
                }
            }
        }
    }

    /// <summary>
    /// Clean up old cache files for a specific assembly name.
    /// Only deletes cache files that are not the current cache key and are older than 7 days.
    /// </summary>
    private void CleanupOldCaches(string assemblyName, string currentCacheKey)
    {
        var pattern = $"{assemblyName.ToLowerInvariant()}-*.json.gz";
        var oldFiles = Directory.GetFiles(_cacheDirectory, pattern);
        var threshold = TimeSpan.FromDays(7);
        var now = DateTime.UtcNow;

        foreach (var file in oldFiles)
        {
            var fileName = Path.GetFileName(file);
            if (fileName == currentCacheKey)
                continue;

            try
            {
                var lastWrite = File.GetLastWriteTimeUtc(file);
                if (now - lastWrite > threshold)
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to delete old cache file '{file}': {ex.Message}", 0, 0);
            }
        }
    }

    /// <summary>
    /// Get information about the cache.
    /// </summary>
    public CacheInfo GetInfo()
    {
        if (!Directory.Exists(_cacheDirectory))
        {
            return new CacheInfo
            {
                CacheDirectory = _cacheDirectory,
                CachedAssemblies = 0,
                TotalSizeBytes = 0
            };
        }

        var files = Directory.GetFiles(_cacheDirectory, "*.json.gz");
        var totalSize = files.Sum(f => new FileInfo(f).Length);

        return new CacheInfo
        {
            CacheDirectory = _cacheDirectory,
            CachedAssemblies = files.Length,
            TotalSizeBytes = totalSize
        };
    }
}

/// <summary>
/// Information about the cache state.
/// </summary>
internal class CacheInfo
{
    public string CacheDirectory { get; set; } = string.Empty;
    public int CachedAssemblies { get; set; }
    public long TotalSizeBytes { get; set; }
}
