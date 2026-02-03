namespace Sharpy.Compiler.Utilities;

/// <summary>
/// Provides consistent path normalization across the compiler.
/// Used for cache keys, symbol storage, and cross-platform file comparison.
/// </summary>
/// <remarks>
/// This utility consolidates path normalization that was previously duplicated
/// across multiple files. All components that need to compare or store file paths
/// should use this class to ensure consistent behavior.
/// </remarks>
public static class PathNormalizer
{
    /// <summary>
    /// Normalizes a file path for use as cache keys and cross-platform comparison.
    /// </summary>
    /// <param name="path">The path to normalize. Can be relative or absolute.</param>
    /// <returns>A normalized, absolute path suitable for comparison and storage.</returns>
    /// <remarks>
    /// The normalization process:
    /// <list type="bullet">
    /// <item>Resolves to absolute path (handles relative paths like "./foo" or "../bar")</item>
    /// <item>Converts backslashes to forward slashes for cross-platform consistency</item>
    /// <item>Lowercases on case-insensitive filesystems (Windows, macOS)</item>
    /// </list>
    /// </remarks>
    public static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Always resolve to absolute path first - this is critical for consistent cache keys
        var normalized = Path.GetFullPath(path).Replace('\\', '/');

        // Case-insensitive on Windows and macOS, case-sensitive on Linux
        if (!OperatingSystem.IsLinux())
        {
            normalized = normalized.ToLowerInvariant();
        }

        return normalized;
    }

    /// <summary>
    /// Gets a relative path from one path to another, normalized.
    /// </summary>
    /// <param name="relativeTo">The base path to compute relative from.</param>
    /// <param name="path">The target path.</param>
    /// <returns>A normalized relative path with forward slashes.</returns>
    public static string GetRelative(string relativeTo, string path)
    {
        var relative = Path.GetRelativePath(relativeTo, path).Replace('\\', '/');
        return relative;
    }
}
