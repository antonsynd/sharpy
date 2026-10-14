extern alias SharpyRT;

using Sharpy.Compiler;

namespace Sharpy.Cli;

/// <summary>
/// Copies the runtime dependencies (Sharpy.Core.dll, used stdlib assemblies, every explicit user
/// reference, and their transitive dependencies — managed assemblies and native assets alike) needed to
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
    /// assembly AND every explicit user reference (managed assemblies and native assets, flat) into
    /// <paramref name="outputDir"/>. Returns the set of copied file names, relative to the output
    /// directory.
    ///
    /// <para>
    /// The deployment set is the used closure ∪ every explicit user reference (#2223). A user
    /// reference (<c>-r</c>, a <c>.spyproj</c> <c>&lt;Reference&gt;</c>) is an explicit input, so it is
    /// deployed whether or not the program "used" it: <paramref name="usedAssemblyPaths"/> never
    /// records a referenced Sharpy library, whose imports resolve through the CLR-namespace arms
    /// that record no usage, so seeding from it alone left the program unable to load its own
    /// reference (FileNotFoundException at <c>dotnet out.dll</c>). Each caller forms
    /// <paramref name="userReferences"/> from its own inputs; a path that names no file is skipped,
    /// as the resolver skips any missing seed.
    /// </para>
    ///
    /// <para>
    /// There is deliberately no name-keyed counterpart that deletes them again: the copies land
    /// under fixed file names, so removing them by name from a directory shared with another
    /// compilation deletes that one's dependencies too. A caller staging into a temporary location
    /// gives that location its own directory and removes the directory (#1419).
    /// </para>
    /// </summary>
    internal static HashSet<string> CopyRuntimeDependencies(
        string outputDir,
        IReadOnlySet<string> usedAssemblyPaths,
        IReadOnlyCollection<string> userReferences)
    {
        var copiedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Sharpy.Core.dll is always required. Locate it via the loaded assembly so its directory
        // (the CLI's output directory) is also where the stdlib assemblies and their NuGet
        // dependencies — managed and native — sit as siblings for the resolver to walk.
        var sharpyCorePath = typeof(SharpyRT::Sharpy.Builtins).Assembly.Location;
        var cliDir = Path.GetDirectoryName(sharpyCorePath)!;

        CopyFile(sharpyCorePath, outputDir, "Sharpy.Core.dll");
        copiedFiles.Add("Sharpy.Core.dll");

        // Seed the closure with Sharpy.Core plus every used stdlib assembly plus every user
        // reference; the resolver walks each one's transitive managed references (MathNet,
        // SQLitePCLRaw, YamlDotNet, a library's own siblings, …) and reports native assets
        // (e.g. e_sqlite3) shipped beside them under runtimes/<rid>/native/.
        var seeds = new List<string>(usedAssemblyPaths);
        seeds.AddRange(userReferences);
        seeds.Add(sharpyCorePath);
        var closure = RuntimeClosureResolver.Resolve(seeds);

        // One source per file name. A user library deployed by an earlier `compile` carries its own
        // copies of first-party assemblies beside it; the program was compiled against the CLI's,
        // so the CLI directory's copy wins a file-name collision.
        var managedByFileName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var managedPath in closure.ManagedAssemblies)
        {
            var fileName = Path.GetFileName(managedPath);
            if (fileName.Equals("Sharpy.Core.dll", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!managedByFileName.TryGetValue(fileName, out var existing) || !IsInDirectory(existing, cliDir))
                managedByFileName[fileName] = managedPath;
        }

        foreach (var (fileName, managedPath) in managedByFileName)
        {
            CopyFile(managedPath, outputDir, fileName);
            copiedFiles.Add(fileName);
        }

        // Native assets probe the app directory at runtime, so they are copied flat beside the
        // output rather than into a runtimes/<rid>/native/ layout.
        foreach (var nativePath in closure.NativeAssets)
        {
            var fileName = Path.GetFileName(nativePath);
            CopyFile(nativePath, outputDir, fileName);
            copiedFiles.Add(fileName);
        }

        return copiedFiles;
    }

    /// <summary>
    /// Copies <paramref name="sourcePath"/> to <paramref name="fileName"/> in
    /// <paramref name="outputDir"/>, unless it already IS that file — a user reference may sit in
    /// the output directory itself (<c>-r out/Lib.dll -o out/main.dll</c>), and copying a file onto
    /// itself fails.
    /// </summary>
    static void CopyFile(string sourcePath, string outputDir, string fileName)
    {
        var destination = Path.GetFullPath(Path.Combine(outputDir, fileName));
        if (string.Equals(Path.GetFullPath(sourcePath), destination, StringComparison.OrdinalIgnoreCase))
            return;

        File.Copy(sourcePath, destination, overwrite: true);
    }

    static bool IsInDirectory(string path, string directory) =>
        string.Equals(
            Path.GetDirectoryName(Path.GetFullPath(path))?.TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
