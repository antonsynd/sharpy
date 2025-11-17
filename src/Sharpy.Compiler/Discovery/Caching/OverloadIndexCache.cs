using System.IO.Compression;
using System.Text.Json;

namespace Sharpy.Compiler.Discovery.Caching;

/// <summary>
/// Manages persistent caching of overload indexes.
/// Cache location: ~/.sharpy/cache/overload-index/
/// </summary>
public class OverloadIndexCache
{
    private readonly string _cacheDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OverloadIndexCache()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _cacheDirectory = Path.Combine(userHome, ".sharpy", "cache", "overload-index");
        
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    /// <summary>
    /// Try to load a cached index for the given assembly identity.
    /// </summary>
    public OverloadIndex? TryLoad(AssemblyIdentity identity)
    {
        var cacheKey = identity.ToCacheKey();
        var cachePath = Path.Combine(_cacheDirectory, cacheKey);

        if (!File.Exists(cachePath))
            return null;

        try
        {
            using var fileStream = File.OpenRead(cachePath);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            var index = JsonSerializer.Deserialize<OverloadIndex>(gzipStream, JsonOptions);
            
            // Verify the identity matches
            if (index?.Identity.Equals(identity) == true)
            {
                return index;
            }

            // Cache is stale, delete it
            File.Delete(cachePath);
            return null;
        }
        catch (Exception)
        {
            // Cache file is corrupted, delete it
            try 
            { 
                File.Delete(cachePath); 
            } 
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"Failed to delete corrupted cache file '{cachePath}': {ex}"); 
            }
            return null;
        }
    }

    /// <summary>
    /// Save an index to the cache.
    /// </summary>
    public void Save(OverloadIndex index)
    {
        var cacheKey = index.Identity.ToCacheKey();
        var cachePath = Path.Combine(_cacheDirectory, cacheKey);
        
        // Clean up old cache files for this assembly (different versions/hashes)
        CleanupOldCaches(index.Identity.Name);

        try
        {
            using var fileStream = File.Create(cachePath);
            using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
            JsonSerializer.Serialize(gzipStream, index, JsonOptions);
        }
        catch (Exception ex)
        {
            // Log but don't fail - caching is optional
            Console.Error.WriteLine($"Warning: Failed to save cache: {ex.Message}");
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
                    Console.Error.WriteLine($"Warning: Failed to delete cache file '{file}': {ex.Message}"); 
                }
            }
        }
    }

    /// <summary>
    /// Clean up old cache files for a specific assembly name.
    /// </summary>
    private void CleanupOldCaches(string assemblyName)
    {
        var pattern = $"{assemblyName.ToLowerInvariant()}-*.json.gz";
        var oldFiles = Directory.GetFiles(_cacheDirectory, pattern);
        
        foreach (var file in oldFiles)
        {
            try 
            { 
                File.Delete(file); 
            } 
            catch (Exception ex) 
            { 
                Console.Error.WriteLine($"Failed to delete cache file '{file}': {ex.Message}"); 
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
public class CacheInfo
{
    public string CacheDirectory { get; set; } = string.Empty;
    public int CachedAssemblies { get; set; }
    public long TotalSizeBytes { get; set; }
}
