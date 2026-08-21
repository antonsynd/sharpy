namespace Sharpy.Lsp;

/// <summary>
/// Resolves the default compiler reference set at server startup, surfacing deployment faults
/// loudly rather than degrading silently. Mirrors the post-condition discipline established by
/// <c>AssemblyCompiler.ValidateReferenceSet</c> (#1482) on the compile side.
/// </summary>
public static class DefaultReferenceSet
{
    public readonly record struct ResolveResult(
        IReadOnlyList<string> References,
        IReadOnlyList<string> Warnings);

    /// <summary>
    /// Builds the default reference list from the Sharpy.Core assembly location.
    /// Returns resolved paths and any deployment-fault warnings.
    /// </summary>
    /// <param name="coreAssemblyPath">
    /// <see cref="System.Reflection.Assembly.Location"/> of the Sharpy.Core runtime assembly.
    /// Empty for single-file or in-memory deployments.
    /// </param>
    public static ResolveResult Resolve(string coreAssemblyPath)
    {
        var references = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrEmpty(coreAssemblyPath))
        {
            warnings.Add(
                "Sharpy.Core assembly reported an empty location — this usually indicates a "
                + "single-file or in-memory deployment. The language server will run without "
                + "runtime type information; module imports will not resolve.");
            return new ResolveResult(references, warnings);
        }

        references.Add(coreAssemblyPath);

        var coreDir = Path.GetDirectoryName(coreAssemblyPath);
        if (string.IsNullOrEmpty(coreDir))
        {
            warnings.Add(
                $"Could not determine the directory containing {Path.GetFileName(coreAssemblyPath)}. "
                + "Sharpy.Stdlib.dll cannot be located; module imports will not resolve.");
            return new ResolveResult(references, warnings);
        }

        var stdlibPath = Path.Combine(coreDir, "Sharpy.Stdlib.dll");
        if (!File.Exists(stdlibPath))
        {
            warnings.Add(
                $"Sharpy.Stdlib.dll not found at {stdlibPath}. This is a deployment fault — "
                + "the standard library should be co-located with Sharpy.Core.dll. Module imports "
                + "(import math, import os, …) will not resolve until the file is restored.");
        }
        else
        {
            references.Add(stdlibPath);
        }

        return new ResolveResult(references, warnings);
    }
}
