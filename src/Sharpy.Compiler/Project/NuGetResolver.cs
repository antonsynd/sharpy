using System.Xml.Linq;
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
    /// Resolves a NuGet package to its assembly DLL paths, including transitive dependencies.
    /// For each package, searches ~/.nuget/packages/{name}/{version}/lib/{tfm}/ for DLLs.
    /// If the package has no DLLs (meta-package), parses its .nuspec to resolve transitive dependencies.
    /// </summary>
    public static List<string> ResolvePackage(PackageRef packageRef, string targetFramework, ICompilerLogger? logger = null)
    {
        return ResolvePackage(packageRef, targetFramework, logger, nugetPackagesDir: null);
    }

    /// <summary>
    /// Resolves a NuGet package to its assembly DLL paths, including transitive dependencies.
    /// Overload that accepts a custom packages directory for testing.
    /// </summary>
    internal static List<string> ResolvePackage(PackageRef packageRef, string targetFramework, ICompilerLogger? logger, string? nugetPackagesDir)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        ResolvePackageTransitive(packageRef, targetFramework, logger, nugetPackagesDir, visited, result);
        return result;
    }

    /// <summary>
    /// Recursively resolves a package and its transitive dependencies from .nuspec files.
    /// Uses a visited set to prevent infinite loops from circular dependencies.
    /// </summary>
    private static void ResolvePackageTransitive(
        PackageRef packageRef,
        string targetFramework,
        ICompilerLogger? logger,
        string? nugetPackagesDir,
        HashSet<string> visited,
        List<string> result)
    {
        // Cycle guard: skip packages already visited
        var packageKey = $"{packageRef.Name.ToLowerInvariant()}/{packageRef.Version}";
        if (!visited.Add(packageKey))
            return;

        var packagesDir = nugetPackagesDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages");

        var packageDir = Path.Combine(packagesDir, packageRef.Name.ToLowerInvariant(), packageRef.Version);
        if (!Directory.Exists(packageDir))
        {
            logger?.LogWarning($"NuGet package not found in global cache: {packageRef.Name} {packageRef.Version}. Run 'dotnet restore' to download it.", 0, 0);
            return;
        }

        // Try to find DLLs in lib/ or ref/ directories
        var directDlls = ResolveDirectDlls(packageDir, targetFramework, packageRef.Name, logger);
        result.AddRange(directDlls);

        // Always parse .nuspec for transitive dependencies, even if we found DLLs.
        // Meta-packages (like xunit) have no DLLs but declare dependencies.
        // Regular packages may also have dependencies that provide additional types.
        var nuspecPath = Path.Combine(packageDir, $"{packageRef.Name.ToLowerInvariant()}.nuspec");
        if (File.Exists(nuspecPath))
        {
            var transitiveDeps = ParseNuspecDependencies(nuspecPath, targetFramework, logger);
            foreach (var dep in transitiveDeps)
            {
                ResolvePackageTransitive(dep, targetFramework, logger, nugetPackagesDir, visited, result);
            }
        }
    }

    /// <summary>
    /// Resolves direct DLLs from a package's lib/ or ref/ directory.
    /// </summary>
    private static List<string> ResolveDirectDlls(string packageDir, string targetFramework, string packageName, ICompilerLogger? logger)
    {
        var dlls = new List<string>();

        var libDir = Path.Combine(packageDir, "lib");
        if (!Directory.Exists(libDir))
        {
            var refDir = Path.Combine(packageDir, "ref");
            if (Directory.Exists(refDir))
                libDir = refDir;
            else
                return dlls;
        }

        var tfmDir = FindBestTfmDirectory(libDir, targetFramework);
        if (tfmDir == null)
        {
            logger?.LogDebug($"No compatible TFM found for {packageName} in {libDir}");
            return dlls;
        }

        foreach (var dll in Directory.GetFiles(tfmDir, "*.dll"))
        {
            dlls.Add(dll);
        }

        return dlls;
    }

    /// <summary>
    /// Parses a .nuspec file and extracts dependency references for the target framework.
    /// Handles both grouped (with targetFramework attribute) and ungrouped dependency formats.
    /// </summary>
    internal static List<PackageRef> ParseNuspecDependencies(string nuspecPath, string targetFramework, ICompilerLogger? logger)
    {
        var dependencies = new List<PackageRef>();

        try
        {
            var doc = XDocument.Load(nuspecPath);
            var root = doc.Root;
            if (root == null)
                return dependencies;

            // .nuspec files use an XML namespace
            var ns = root.Name.Namespace;

            var metadata = root.Element(ns + "metadata");
            if (metadata == null)
                return dependencies;

            var depsElement = metadata.Element(ns + "dependencies");
            if (depsElement == null)
                return dependencies;

            // Check for <group> elements (TFM-specific dependencies)
            var groups = depsElement.Elements(ns + "group").ToList();
            if (groups.Count > 0)
            {
                // Find the best matching group for our target framework
                var matchedGroup = FindBestTfmGroup(groups, ns, targetFramework);
                if (matchedGroup != null)
                {
                    foreach (var dep in matchedGroup.Elements(ns + "dependency"))
                    {
                        var depRef = ParseDependencyElement(dep);
                        if (depRef != null)
                            dependencies.Add(depRef);
                    }
                }
            }
            else
            {
                // Ungrouped format: <dependency> elements directly under <dependencies>
                foreach (var dep in depsElement.Elements(ns + "dependency"))
                {
                    var depRef = ParseDependencyElement(dep);
                    if (depRef != null)
                        dependencies.Add(depRef);
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug($"Failed to parse .nuspec at {nuspecPath}: {ex.Message}");
        }

        return dependencies;
    }

    /// <summary>
    /// Parses a single &lt;dependency&gt; element into a PackageRef.
    /// Handles version range syntax like "[2.9.3]" by extracting the version number.
    /// </summary>
    private static PackageRef? ParseDependencyElement(XElement dep)
    {
        var id = dep.Attribute("id")?.Value;
        var version = dep.Attribute("version")?.Value;

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
            return null;

        // Strip version range brackets: "[2.9.3]" -> "2.9.3", "(,2.0)" -> ",2.0"
        // For ranges like "[1.0.0, 2.0.0)", use the lower bound
        version = NormalizeVersion(version);

        return new PackageRef(id, version);
    }

    /// <summary>
    /// Normalizes a NuGet version range to a concrete version string.
    /// "[2.9.3]" -> "2.9.3", "2.9.3" -> "2.9.3", "[1.0.0, 2.0.0)" -> "1.0.0"
    /// </summary>
    internal static string NormalizeVersion(string version)
    {
        version = version.Trim();

        // Strip leading bracket/paren
        if (version.StartsWith('[') || version.StartsWith('('))
            version = version[1..];

        // Strip trailing bracket/paren
        if (version.EndsWith(']') || version.EndsWith(')'))
            version = version[..^1];

        // If it's a range with comma, take the lower bound
        var commaIndex = version.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex >= 0)
        {
            var lowerBound = version[..commaIndex].Trim();
            if (!string.IsNullOrEmpty(lowerBound))
                return lowerBound;

            // If lower bound is empty (e.g., "(,2.0)"), use the upper bound
            return version[(commaIndex + 1)..].Trim();
        }

        return version.Trim();
    }

    /// <summary>
    /// Finds the best matching &lt;group&gt; element for the target framework.
    /// Tries exact match, then compatible fallbacks, then the default (no TFM) group.
    /// </summary>
    private static XElement? FindBestTfmGroup(List<XElement> groups, XNamespace ns, string targetFramework)
    {
        // Map TFM short names to .nuspec targetFramework attribute values
        // .nuspec uses various formats: ".NETStandard2.0", "net8.0", ".NETFramework4.6.2", etc.
        var tfmAliases = BuildTfmAliases(targetFramework);

        // Try each compatible TFM in preference order
        foreach (var tfm in tfmAliases)
        {
            foreach (var group in groups)
            {
                var groupTfm = group.Attribute("targetFramework")?.Value;
                if (groupTfm != null && groupTfm.Equals(tfm, StringComparison.OrdinalIgnoreCase))
                    return group;
            }
        }

        // Fall back to the default group (no targetFramework attribute)
        var defaultGroup = groups.FirstOrDefault(g => g.Attribute("targetFramework") == null);
        return defaultGroup;
    }

    /// <summary>
    /// Builds a prioritized list of TFM aliases for matching .nuspec group targetFramework values.
    /// </summary>
    private static List<string> BuildTfmAliases(string targetFramework)
    {
        var aliases = new List<string>();

        // The exact target framework first
        aliases.Add(targetFramework);

        // Build a fallback chain from most specific to least specific
        var compatibleTfms = new[]
        {
            "net10.0", "net9.0", "net8.0", "net7.0", "net6.0",
            "netstandard2.1", "netstandard2.0",
            "netcoreapp3.1", "netcoreapp3.0",
        };

        // Add compatible TFMs that come after the target in preference order
        bool foundTarget = false;
        foreach (var tfm in compatibleTfms)
        {
            if (tfm.Equals(targetFramework, StringComparison.OrdinalIgnoreCase))
            {
                foundTarget = true;
                continue; // Already added above
            }
            if (foundTarget)
                aliases.Add(tfm);
        }

        // If the target wasn't in our list, add all fallbacks
        if (!foundTarget)
        {
            aliases.AddRange(compatibleTfms);
        }

        // Also add long-form aliases used in .nuspec files
        if (targetFramework.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
        {
            var version = targetFramework["netstandard".Length..];
            aliases.Add($".NETStandard{version}");
        }

        // Common .nuspec long-form names
        aliases.Add(".NETStandard2.1");
        aliases.Add(".NETStandard2.0");

        return aliases;
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
