using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Sharpy.Compiler.Discovery.Caching;

/// <summary>
/// Uniquely identifies an assembly for caching purposes.
/// Includes version and content hash to detect changes.
/// </summary>
public class AssemblyIdentity
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Create an identity from an assembly file path.
    /// </summary>
    public static AssemblyIdentity FromAssemblyPath(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        var assemblyName = assembly.GetName();
        
        return new AssemblyIdentity
        {
            Name = assemblyName.Name ?? Path.GetFileNameWithoutExtension(assemblyPath),
            Version = assemblyName.Version?.ToString() ?? "1.0.0",
            ContentHash = ComputeFileHash(assemblyPath),
            FilePath = assemblyPath
        };
    }

    /// <summary>
    /// Create an identity from a loaded assembly.
    /// </summary>
    public static AssemblyIdentity FromAssembly(Assembly assembly)
    {
        var assemblyName = assembly.GetName();
        var location = assembly.Location;
        
        return new AssemblyIdentity
        {
            Name = assemblyName.Name ?? "Unknown",
            Version = assemblyName.Version?.ToString() ?? "1.0.0",
            ContentHash = string.IsNullOrEmpty(location) ? string.Empty : ComputeFileHash(location),
            FilePath = location
        };
    }

    /// <summary>
    /// Generate a cache key for this assembly.
    /// Format: {name}-{version}-{hash}.json.gz
    /// </summary>
    public string ToCacheKey()
    {
        var hash = ContentHash.Length > 12 ? ContentHash[..12] : ContentHash;
        return $"{Name.ToLowerInvariant()}-{Version}-{hash}.json.gz";
    }

    private static string ComputeFileHash(string filePath)
    {
        if (!File.Exists(filePath))
            return string.Empty;

        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not AssemblyIdentity other)
            return false;

        return Name == other.Name &&
               Version == other.Version &&
               ContentHash == other.ContentHash;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Version, ContentHash);
    }
}
