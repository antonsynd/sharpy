using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Sharpy.Compiler.Discovery.Caching;

/// <summary>
/// Uniquely identifies an assembly for caching purposes.
/// Includes version and content hash to detect changes.
/// </summary>
internal class AssemblyIdentity
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    /// <summary>
    /// Identity of the compiler that built the index. A changed compiler invalidates the
    /// cache by construction — no manual <c>CurrentCacheFormatVersion</c> bump needed (#1313).
    /// </summary>
    public string CompilerVersion { get; set; } = string.Empty;

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
            FilePath = assemblyPath,
            CompilerVersion = Shared.CompilerIdentity.Version
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
            FilePath = location,
            CompilerVersion = Shared.CompilerIdentity.Version
        };
    }

    /// <summary>
    /// Generate a cache key for this assembly.
    /// Format: {name}-{version}-{hash}-c{compilerHash}.json.gz
    /// The compiler component is APPENDED so <c>CleanupOldCaches</c>' glob
    /// <c>{name}-*.json.gz</c> keeps matching (#1313).
    /// </summary>
    public string ToCacheKey()
    {
        var hash = string.IsNullOrEmpty(ContentHash) ? "no-hash" : (ContentHash.Length > 12 ? ContentHash[..12] : ContentHash);
        var compilerTag = string.IsNullOrEmpty(CompilerVersion) ? "c0" : $"c{CompilerVersion}";
        return $"{Name.ToLowerInvariant()}-{Version}-{hash}-{compilerTag}.json.gz";
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
               ContentHash == other.ContentHash &&
               CompilerVersion == other.CompilerVersion;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Version, ContentHash, CompilerVersion);
    }
}
