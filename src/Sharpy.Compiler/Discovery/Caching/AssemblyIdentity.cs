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
            ContentHash = ContentHashOf(assembly, location),
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
    /// The SHA-256 of the file the assembly was loaded from; when there is no such file to read (an
    /// in-memory load, or a cached copy deleted after it was mapped), the image's module version id,
    /// which names the loaded bytes and survives the file. Never empty: an empty hash made the cache
    /// key <c>{name}-{version}-no-hash</c>, shared by every build of that name and version.
    /// </summary>
    private static string ContentHashOf(Assembly assembly, string location)
    {
        if (!string.IsNullOrEmpty(location) && File.Exists(location))
        {
            try
            {
                return Shared.FileContentIdentity.Sha256Of(location);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Unreadable: fall through to the image's own identity.
            }
        }

        return "mvid" + assembly.ManifestModule.ModuleVersionId.ToString("N");
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
