using System.IO.Compression;
using System.Text.Json;
using System.Threading;
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
    // v15: [SharpyModule] class names recorded for module alias resolution (#891).
    // v16: value-type Nullable<T> parameters now serialize via the __nullable__ sentinel (#890).
    // v17: previous format version.
    // v18: TypeParameters changed from List<string> to List<TypeParameterInfo> with CLR constraints (#976).
    internal const int CurrentCacheFormatVersion = 18;

    public CacheStatistics Statistics { get; } = new();

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
    /// Best-effort: returns null on any I/O failure rather than blocking.
    /// </summary>
    public OverloadIndex? TryLoad(AssemblyIdentity identity)
    {
        var cacheKey = identity.ToCacheKey();
        var cachePath = Path.Combine(_cacheDirectory, cacheKey);

        if (!File.Exists(cachePath))
        {
            Statistics.RecordMiss();
            return null;
        }

        try
        {
            using var fileStream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            var index = JsonSerializer.Deserialize<OverloadIndex>(gzipStream, JsonOptions);

            // Reject caches from older format versions
            if (index?.CacheFormatVersion != CurrentCacheFormatVersion)
            {
                TryDeleteFile(cachePath);
                Statistics.RecordMiss();
                return null;
            }

            // Verify the identity matches
            if (index.Identity.Equals(identity))
            {
                Statistics.RecordHit();
                return index;
            }

            // Cache is stale, delete it
            TryDeleteFile(cachePath);
            Statistics.RecordMiss();
            return null;
        }
        catch (IOException ex)
        {
            // File locked by another process — skip rather than block
            _logger.LogDebug($"Cache load skipped (file locked): {ex.Message}");
            Statistics.RecordMiss();
            return null;
        }
        catch (Exception ex)
        {
            // Cache file is corrupted, incompatible, or inaccessible
            _logger.LogDebug($"Failed to load cache from '{cachePath}': {ex.GetType().Name} - {ex.Message}");
            TryDeleteFile(cachePath);
            Statistics.RecordMiss();
            return null;
        }
    }

    /// <summary>
    /// Save an index to the cache.
    /// Uses atomic write (write to temp file, then rename) to prevent corruption.
    /// Best-effort: skips saving on any I/O failure rather than blocking.
    /// </summary>
    public void Save(OverloadIndex index)
    {
        var cacheKey = index.Identity.ToCacheKey();
        var cachePath = Path.Combine(_cacheDirectory, cacheKey);
        var tempPath = cachePath + $".{Guid.NewGuid():N}.tmp";

        // Clean up old cache files for this assembly (different versions/hashes older than 7 days)
        CleanupOldCaches(index.Identity.Name, cacheKey);

        try
        {
            // Write to a temporary file first
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
            {
                JsonSerializer.Serialize(gzipStream, index, JsonOptions);
            }

            // Atomically move the temp file to the final location
            File.Move(tempPath, cachePath, overwrite: true);
        }
        catch (IOException ex)
        {
            // File locked by another process — skip rather than block
            TryDeleteFile(tempPath);
            _logger.LogDebug($"Cache save skipped (file locked): {ex.Message}");
        }
        catch (Exception ex)
        {
            // Clean up temp file on error
            TryDeleteFile(tempPath);
            _logger.LogDebug($"Cache save failed: {ex.Message}");
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
/// Tracks cache hit/miss counts. Thread-safe via Interlocked.
/// </summary>
internal class CacheStatistics
{
    private int _hits;
    private int _misses;

    public int Hits => _hits;
    public int Misses => _misses;

    internal void RecordHit() => Interlocked.Increment(ref _hits);
    internal void RecordMiss() => Interlocked.Increment(ref _misses);
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
