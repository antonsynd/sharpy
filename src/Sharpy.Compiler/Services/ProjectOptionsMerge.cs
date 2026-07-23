using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Services;

/// <summary>
/// The single definition of how compiler-level <see cref="CompilerOptions"/> and
/// project-level <see cref="ProjectConfig"/> settings combine when a <c>.spyproj</c> is
/// compiled or analyzed. Both the compile path (<see cref="Compiler.CompileProject"/>)
/// and the analyze path (<see cref="CompilerApi.AnalyzeProject"/>) route through this
/// helper so the two can never diverge — divergent merges are exactly how #1109 (analyze
/// silently dropping <c>&lt;Features&gt;</c>/warnings config) happened.
/// </summary>
/// <remarks>
/// <see cref="CompilerOptions.MaxErrors"/> is intentionally not part of the merge: it has no
/// project-side counterpart, so it always comes straight from the options.
/// </remarks>
internal static class ProjectOptionsMerge
{
    /// <summary>
    /// Merges the option-level and project-level warning/feature settings:
    /// <list type="bullet">
    ///   <item><c>SuppressedWarnings</c> is the union of both sets (case-insensitive).</item>
    ///   <item><c>WarningsAsErrors</c> is the logical OR of both flags.</item>
    ///   <item><c>Features</c> is the option set with the project's <c>&lt;Features&gt;</c> enabled on top.</item>
    /// </list>
    /// Names are already validated at each boundary (CLI arg parsing and project load), so the
    /// feature union is a pure merge with no re-validation.
    /// </summary>
    public static MergedProjectOptions Merge(CompilerOptions options, ProjectConfig config)
    {
        var suppressed = new HashSet<string>(options.SuppressedWarnings, StringComparer.OrdinalIgnoreCase);
        suppressed.UnionWith(config.SuppressedWarnings);

        var warningsAsErrors = options.WarningsAsErrors || config.WarningsAsErrors;
        var features = options.Features.Enable(config.Features);

        return new MergedProjectOptions(warningsAsErrors, suppressed, features);
    }
}

/// <summary>
/// The merged warning/feature settings produced by <see cref="ProjectOptionsMerge.Merge"/>,
/// consumed by the <see cref="Project.ProjectCompiler"/> constructor on both the compile and
/// analyze paths.
/// </summary>
internal readonly record struct MergedProjectOptions(
    bool WarningsAsErrors, HashSet<string> SuppressedWarnings, FeatureFlags Features);
