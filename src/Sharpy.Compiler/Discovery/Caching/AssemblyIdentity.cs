using System.Reflection;

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
            ContentHash = ImageIdentityOf(assembly),
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

    /// <summary>
    /// The identity of the LOADED image: its module version id, as 32 hex digits. Not a hash of the
    /// file now at <see cref="Assembly.Location"/>: that file can have been replaced since the load (an
    /// index built from the old image would be filed under the new file's hash, #2114) or deleted (a
    /// pruned cache copy; the hash was empty and the key <c>{name}-{version}-no-hash</c> collided across
    /// builds). The MVID names the bytes the runtime mapped, survives the file, and costs no read.
    /// </summary>
    private static string ImageIdentityOf(Assembly assembly)
        => assembly.ManifestModule.ModuleVersionId.ToString("N");

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
