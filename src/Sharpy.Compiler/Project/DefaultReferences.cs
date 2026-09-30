extern alias SharpyRT;

namespace Sharpy.Compiler.Project;

/// <summary>
/// The reference set a Sharpy build discovers modules from and compiles against: <c>Sharpy.Core.dll</c>
/// plus the standard library — the monolith <c>Sharpy.Stdlib.dll</c>, or the per-module
/// <c>Sharpy.Stdlib.*.dll</c> assemblies — resolved next to <c>Sharpy.Core</c>. ONE authority (#2140):
/// <c>sharpyc</c> (<c>CliHelpers.GetDefaultReferences</c>) and the in-process project test harness both
/// read it, so a test takes the import route <c>sharpyc</c> takes. Before #2140 the harness fed only
/// <c>Sharpy.Stdlib.dll</c> into discovery, so Core's attribute-declared modules
/// (<c>sharpy.generators</c>) resolved on the bare-namespace route there and nowhere else.
/// </summary>
internal static class DefaultReferences
{
    /// <summary>
    /// The default references, Core first. <paramref name="onMissingStdlib"/> is told when no stdlib
    /// assembly sits next to Core (stdlib modules will not be importable).
    /// </summary>
    public static string[] Resolve(Action<string>? onMissingStdlib = null)
    {
        var corePath = typeof(SharpyRT::Sharpy.Builtins).Assembly.Location;
        var coreDir = Path.GetDirectoryName(corePath)!;
        var refs = new List<string> { corePath };

        var monolithPath = Path.Combine(coreDir, "Sharpy.Stdlib.dll");
        if (File.Exists(monolithPath))
        {
            refs.Add(monolithPath);
        }
        else
        {
            var perModuleAssemblies = SourceGlob.EnumerateArtifacts(coreDir, "Sharpy.Stdlib.*.dll").ToArray();
            if (perModuleAssemblies.Length > 0)
                refs.AddRange(perModuleAssemblies);
            else
                onMissingStdlib?.Invoke(
                    "Warning: No Sharpy.Stdlib assemblies found next to Sharpy.Core.dll — stdlib modules (json, os, math, etc.) will not be available.");
        }

        return refs.ToArray();
    }

    /// <summary>
    /// Adds <paramref name="defaults"/> to the project's own references, so the emitted assembly's
    /// compilation resolves Core and Stdlib types the way module discovery does.
    /// </summary>
    public static void ApplyTo(ProjectConfig config, IEnumerable<string> defaults)
    {
        foreach (var reference in defaults)
        {
            if (!config.References.Contains(reference))
                config.References.Add(reference);
        }
    }
}
