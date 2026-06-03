using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Project;

/// <summary>
/// Resolves NuGet package references to assembly DLL paths from the global packages cache.
/// Used by both AssemblyCompiler (for Roslyn MetadataReferences) and CompilerApi
/// (for ModuleRegistry during semantic analysis).
/// </summary>
internal static class NuGetResolver
{
    /// <summary>
    /// Resolves a NuGet package to its assembly DLL paths.
    /// Searches ~/.nuget/packages/{name}/{version}/lib/{tfm}/ for DLLs.
    /// </summary>
    public static List<string> ResolvePackage(PackageRef packageRef, string targetFramework, ICompilerLogger? logger = null)
    {
        var result = new List<string>();
        var nugetPackagesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages");

        var packageDir = Path.Combine(nugetPackagesDir, packageRef.Name.ToLowerInvariant(), packageRef.Version);
        if (!Directory.Exists(packageDir))
        {
            logger?.LogWarning($"NuGet package not found in global cache: {packageRef.Name} {packageRef.Version}. Run 'dotnet restore' to download it.", 0, 0);
            return result;
        }

        var libDir = Path.Combine(packageDir, "lib");
        if (!Directory.Exists(libDir))
        {
            var refDir = Path.Combine(packageDir, "ref");
            if (Directory.Exists(refDir))
                libDir = refDir;
            else
                return result;
        }

        var tfmDir = FindBestTfmDirectory(libDir, targetFramework);
        if (tfmDir == null)
        {
            logger?.LogDebug($"No compatible TFM found for {packageRef.Name} in {libDir}");
            return result;
        }

        foreach (var dll in Directory.GetFiles(tfmDir, "*.dll"))
        {
            result.Add(dll);
        }

        return result;
    }

    /// <summary>
    /// Finds the best matching target framework directory.
    /// Tries exact match first, then falls back to compatible frameworks.
    /// </summary>
    public static string? FindBestTfmDirectory(string libDir, string targetFramework)
    {
        var exactPath = Path.Combine(libDir, targetFramework);
        if (Directory.Exists(exactPath))
            return exactPath;

        var compatibleTfms = new[]
        {
            targetFramework,
            "net10.0", "net9.0", "net8.0", "net7.0", "net6.0",
            "netstandard2.1", "netstandard2.0",
            "netcoreapp3.1", "netcoreapp3.0",
        };

        foreach (var tfm in compatibleTfms)
        {
            var path = Path.Combine(libDir, tfm);
            if (Directory.Exists(path))
                return path;
        }

        var directories = Directory.GetDirectories(libDir);
        return directories.FirstOrDefault(d => Directory.GetFiles(d, "*.dll").Length > 0);
    }
}
