using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Services;

/// <summary>
/// The single definition of how compiler-level <see cref="CompilerOptions"/> and project-level
/// <see cref="ProjectConfig"/> settings combine into the flag set
/// <see cref="Project.ProjectCompiler"/> runs with. Every construction of a
/// <see cref="Project.ProjectCompiler"/> goes through here, so the compile path
/// (<see cref="Compiler.CompileProject"/>), the analyze paths
/// (<see cref="CompilerApi.AnalyzeProject"/>, <c>SyntheticProject.Analyze</c>), the synthetic
/// project-of-one-file, and the REPL can never diverge — divergent threading is exactly how #1109
/// (analyze silently dropping <c>&lt;Features&gt;</c>/warnings config) happened.
/// </summary>
/// <remarks>
/// This is the third surface of the options seam (#1144). The compiler used to take these five
/// settings as loose constructor parameters, so each call site re-threaded the list by hand and a
/// new flag had to be remembered N times; they now travel as one <see cref="ProjectCompilerOptions"/>
/// value produced only here.
/// </remarks>
internal static class ProjectOptionsMerge
{
    /// <summary>
    /// Derives the compiler's flag set from the caller's options, optionally widened by a project's
    /// own settings:
    /// <list type="bullet">
    ///   <item><c>SuppressedWarnings</c> is the union of both sets (case-insensitive).</item>
    ///   <item><c>WarningsAsErrors</c> is the logical OR of both flags.</item>
    ///   <item><c>Features</c> is the option set with the project's <c>&lt;Features&gt;</c> enabled on top.</item>
    ///   <item><c>MaxErrors</c> comes straight from the options — it has no project-side counterpart.</item>
    ///   <item><c>Incremental</c> comes from the options unless <paramref name="incremental"/> overrides it.</item>
    /// </list>
    /// Names are already validated at each boundary (CLI arg parsing and project load), so the
    /// feature union is a pure merge with no re-validation.
    /// </summary>
    /// <param name="options">The caller's compiler options.</param>
    /// <param name="config">
    /// The project whose settings widen the options, or null for the paths whose config was itself
    /// built from these options (the synthetic project-of-one-file and the REPL) and so has nothing
    /// independent to contribute.
    /// </param>
    /// <param name="incremental">
    /// Overrides <see cref="CompilerOptions.Incremental"/>. The analyze paths and the synthetic
    /// project-of-one-file pass <c>false</c>: the incremental cache is keyed on a real project's
    /// <c>obj/</c> directory and neither emits an assembly.
    /// </param>
    /// <param name="sharedBuiltins">
    /// A cached <see cref="Semantic.Registry.BuiltinRegistry"/> the analyze paths share across
    /// analyses (#1140), or null to have the compiler construct a fresh one.
    /// </param>
    public static ProjectCompilerOptions Merge(
        CompilerOptions options, ProjectConfig? config = null, bool? incremental = null,
        Semantic.Registry.BuiltinRegistry? sharedBuiltins = null)
    {
        var suppressed = new HashSet<string>(options.SuppressedWarnings, StringComparer.OrdinalIgnoreCase);
        var warningsAsErrors = options.WarningsAsErrors;
        var features = options.Features;

        if (config != null)
        {
            suppressed.UnionWith(config.SuppressedWarnings);
            warningsAsErrors = warningsAsErrors || config.WarningsAsErrors;
            features = features.Enable(config.Features);
        }

        return new ProjectCompilerOptions(
            warningsAsErrors, suppressed, options.MaxErrors,
            incremental ?? options.Incremental, features, sharedBuiltins);
    }
}

/// <summary>
/// The whole flag set <see cref="Project.ProjectCompiler"/> is constructed with, produced only by
/// <see cref="ProjectOptionsMerge.Merge"/>. Adding a setting the compiler must honor means adding a
/// member here, which the compiler and every entry point then get from the one merge.
/// </summary>
internal readonly record struct ProjectCompilerOptions(
    bool WarningsAsErrors, HashSet<string>? SuppressedWarnings, int MaxErrors,
    bool Incremental, FeatureFlags? Features,
    Semantic.Registry.BuiltinRegistry? SharedBuiltinRegistry = null)
{
    /// <summary>
    /// The all-defaults set, for callers with no options of their own (tests and tools that just
    /// want a compiler). A fresh instance per access: the suppressed-warning set is handed to the
    /// compiler by reference and must not be shared between compilations.
    /// </summary>
    public static ProjectCompilerOptions Default =>
        new(false, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0, false, FeatureFlags.None);
}
