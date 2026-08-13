extern alias SharpyRT;

using Sharpy.Compiler;

namespace Sharpy.Cli;

/// <summary>
/// Copies the runtime dependencies (Sharpy.Core.dll, used stdlib assemblies, and their
/// transitive NuGet dependencies — managed assemblies and native assets alike) needed to
/// execute a compiled Sharpy assembly via <c>dotnet output.dll</c>. Shared by the <c>run</c>
/// and <c>compile</c> commands.
///
/// <para>
/// The dependency set is computed mechanically by <see cref="RuntimeClosureResolver"/> from the
/// used assemblies' metadata, replacing the hand-maintained NuGet map that had drifted (it
/// dropped YamlDotNet entirely and copied no native assets). One source of truth, shared with
/// <c>deps.json</c> generation, so this class of drift cannot recur (#1084).
/// </para>
/// </summary>
internal static class RuntimeDependencyHelper
{
    /// <summary>
    /// Copies Sharpy.Core.dll plus the full transitive runtime closure of every used stdlib
    /// assembly (managed assemblies and native assets, flat) into <paramref name="outputDir"/>.
    /// Returns the set of copied file names, relative to the output directory.
    ///
    /// <para>
    /// There is deliberately no name-keyed counterpart that deletes them again: the copies land
    /// under fixed file names, so removing them by name from a directory shared with another
    /// compilation deletes that one's dependencies too. A caller staging into a temporary location
    /// gives that location its own directory and removes the directory (#1419).
    /// </para>
    /// </summary>
    internal static HashSet<string> CopyRuntimeDependencies(string outputDir, IReadOnlySet<string> usedAssemblyPaths)
    {
        var copiedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Sharpy.Core.dll is always required. Locate it via the loaded assembly so its directory
        // (the CLI's output directory) is also where the stdlib assemblies and their NuGet
        // dependencies — managed and native — sit as siblings for the resolver to walk.
        var sharpyCorePath = typeof(SharpyRT::Sharpy.Builtins).Assembly.Location;

        File.Copy(sharpyCorePath, Path.Combine(outputDir, "Sharpy.Core.dll"), overwrite: true);
        copiedFiles.Add("Sharpy.Core.dll");

        // Seed the closure with Sharpy.Core plus every used stdlib assembly; the resolver walks
        // each one's transitive managed references (MathNet, SQLitePCLRaw, YamlDotNet, …) and
        // reports native assets (e.g. e_sqlite3) shipped beside them under runtimes/<rid>/native/.
        var seeds = new List<string>(usedAssemblyPaths) { sharpyCorePath };
        var closure = RuntimeClosureResolver.Resolve(seeds);

        foreach (var managedPath in closure.ManagedAssemblies)
        {
            var fileName = Path.GetFileName(managedPath);
            if (fileName.Equals("Sharpy.Core.dll", StringComparison.OrdinalIgnoreCase))
                continue;

            File.Copy(managedPath, Path.Combine(outputDir, fileName), overwrite: true);
            copiedFiles.Add(fileName);
        }

        // Native assets probe the app directory at runtime, so they are copied flat beside the
        // output rather than into a runtimes/<rid>/native/ layout.
        foreach (var nativePath in closure.NativeAssets)
        {
            var fileName = Path.GetFileName(nativePath);
            File.Copy(nativePath, Path.Combine(outputDir, fileName), overwrite: true);
            copiedFiles.Add(fileName);
        }

        return copiedFiles;
    }
}
