using System.Security.Cryptography;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Single memoized source of compiler identity for cache invalidation.
/// Both <c>IncrementalCompilationCache</c> and <c>OverloadIndexCache</c> consume this
/// so that a changed compiler invalidates all caches by construction (#1313).
/// </summary>
internal static class CompilerIdentity
{
    private static readonly Lazy<string> s_version = new(ComputeVersion);

    /// <summary>
    /// "{assemblyVersion}-{sha256first16}" — one SHA-256 read per process lifetime.
    /// </summary>
    public static string Version => s_version.Value;

    private static string ComputeVersion()
    {
        var assembly = typeof(CompilerIdentity).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0";

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
}
