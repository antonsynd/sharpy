extern alias SharpyRT;

namespace Sharpy.Cli;

/// <summary>
/// Copies the runtime dependencies (Sharpy.Core.dll, used stdlib assemblies, and
/// their transitive NuGet dependencies) needed to execute a compiled Sharpy
/// assembly via <c>dotnet output.dll</c>. Shared by the <c>run</c> and
/// <c>compile</c> commands.
/// </summary>
internal static class RuntimeDependencyHelper
{
    private static readonly string[] NumpyNuGetDeps = new[] { "MathNet.Numerics.dll" };
    private static readonly string[] Sqlite3NuGetDeps = new[]
    {
        "Microsoft.Data.Sqlite.dll",
        "SQLitePCLRaw.batteries_v2.dll",
        "SQLitePCLRaw.core.dll",
        "SQLitePCLRaw.provider.e_sqlite3.dll",
    };
    private static readonly string[] TomlNuGetDeps = new[] { "Tomlyn.dll" };

    private static readonly Dictionary<string, string[]> PerModuleNuGetDeps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sharpy.Stdlib.Numpy.dll"] = NumpyNuGetDeps,
        ["Sharpy.Stdlib.Sqlite3.dll"] = Sqlite3NuGetDeps,
        ["Sharpy.Stdlib.Toml.dll"] = TomlNuGetDeps,
        ["Sharpy.Stdlib.dll"] = NumpyNuGetDeps.Concat(Sqlite3NuGetDeps).Concat(TomlNuGetDeps).ToArray(),
    };

    /// <summary>
    /// Copies Sharpy.Core.dll plus every used stdlib assembly and its transitive
    /// NuGet dependencies into <paramref name="outputDir"/>. Returns the set of
    /// copied file names (relative to the output directory) so callers can clean
    /// them up later.
    /// </summary>
    internal static HashSet<string> CopyRuntimeDependencies(string outputDir, IReadOnlySet<string> usedAssemblyPaths)
    {
        var copiedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Sharpy.Core.dll is always required. Locate it via the loaded assembly so
        // the source directory (the CLI's output directory) also hosts the stdlib
        // assemblies and their NuGet dependencies.
        var sharpyCorePath = typeof(SharpyRT::Sharpy.Builtins).Assembly.Location;
        var cliDir = Path.GetDirectoryName(sharpyCorePath)!;

        File.Copy(sharpyCorePath, Path.Combine(outputDir, "Sharpy.Core.dll"), overwrite: true);
        copiedFiles.Add("Sharpy.Core.dll");

        foreach (var assemblyPath in usedAssemblyPaths)
        {
            var fileName = Path.GetFileName(assemblyPath);
            if (fileName.Equals("Sharpy.Core.dll", StringComparison.OrdinalIgnoreCase))
                continue;

            CopyRuntimeDependency(cliDir, outputDir, fileName);
            copiedFiles.Add(fileName);

            if (PerModuleNuGetDeps.TryGetValue(fileName, out var nugetDeps))
            {
                foreach (var dep in nugetDeps)
                {
                    CopyRuntimeDependency(cliDir, outputDir, dep);
                    copiedFiles.Add(dep);
                }
            }
        }

        return copiedFiles;
    }

    /// <summary>
    /// Deletes the runtime dependency files previously copied into
    /// <paramref name="dir"/> (as returned by <see cref="CopyRuntimeDependencies"/>).
    /// </summary>
    internal static void CleanupRuntimeDependencies(string dir, IReadOnlySet<string>? copiedFiles)
    {
        if (copiedFiles == null)
            return;

        foreach (var dep in copiedFiles)
        {
            var path = Path.Combine(dir, dep);
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void CopyRuntimeDependency(string sourceDir, string destDir, string fileName)
    {
        var src = Path.Combine(sourceDir, fileName);
        if (File.Exists(src))
            File.Copy(src, Path.Combine(destDir, fileName), overwrite: true);
    }
}
