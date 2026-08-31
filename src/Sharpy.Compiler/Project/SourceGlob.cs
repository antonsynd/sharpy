using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Project;

internal static class SourceGlob
{
    internal static IEnumerable<string> EnumerateSourceFiles(
        string baseDir, string pattern, SearchOption option)
    {
        return Directory.EnumerateFiles(baseDir, pattern, option)
            .Where(f => !CrashBundleWriter.IsNonSourceSegment(
                Path.GetRelativePath(baseDir, f)));
    }

    internal static IEnumerable<string> EnumerateSourceDirectories(
        string baseDir, string pattern, SearchOption option)
    {
        return Directory.EnumerateDirectories(baseDir, pattern, option)
            .Where(d => !CrashBundleWriter.IsNonSourceSegment(
                Path.GetRelativePath(baseDir, d)));
    }

    internal static List<string> ResolveGlob(
        string baseDir, string includePattern, string? excludePattern = null)
    {
        var matcher = new Matcher();
        matcher.AddInclude(includePattern);

        if (!string.IsNullOrWhiteSpace(excludePattern))
        {
            var excludePatterns = excludePattern.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pattern in excludePatterns)
            {
                matcher.AddExclude(pattern.Trim());
            }
        }

        var directoryInfo = new DirectoryInfo(baseDir);
        var result = matcher.Execute(new DirectoryInfoWrapper(directoryInfo));

        return result.Files
            .Select(f => Path.GetFullPath(Path.Combine(baseDir, f.Path)))
            .Where(File.Exists)
            .Where(f => !CrashBundleWriter.IsNonSourceSegment(
                Path.GetRelativePath(baseDir, f)))
            .ToList();
    }

    internal static IEnumerable<string> EnumerateArtifacts(string dir, string pattern)
    {
        return Directory.EnumerateFiles(dir, pattern);
    }

    internal static string[] EnumerateArtifactDirectories(string dir)
    {
        return Directory.GetDirectories(dir);
    }
}
