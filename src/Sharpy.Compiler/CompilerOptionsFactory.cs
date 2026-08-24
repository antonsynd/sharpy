using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler;

/// <summary>
/// The single place a <see cref="CompilerOptions"/> instance is constructed (#1144).
/// </summary>
/// <remarks>
/// <para>
/// Options construction used to be per-entry-point: the CLI, <see cref="CompilerApi"/>, the
/// <see cref="Compiler"/> facade, the LSP workspace, and the playground each hand-assembled their
/// own object initializer. Every new flag therefore had to be threaded N times and was forgotten at
/// least once — #1097 (<c>emit csharp</c> dropped <c>--enable-feature</c>) and #1109 (the LSP
/// <c>.spyproj</c> path dropped warnings/maxErrors/features) are the same defect one batch apart.
/// </para>
/// <para>
/// Every member of <see cref="CompilerOptions"/> is assigned exactly once, in
/// <see cref="Create"/>, from an explicit <see cref="CompilerOptionsSpec"/> field. Adding a member
/// to <see cref="CompilerOptions"/> without touching this file fails
/// <c>CompilerOptionsSeamConformanceTests.FactoryCore_AssignsEverySettableCompilerOptionsMember</c>,
/// and constructing <see cref="CompilerOptions"/> anywhere else in the product fails
/// <c>CompilerOptionsSeamConformanceTests.CompilerOptions_IsConstructedOnlyBySeam</c>. The author of a
/// new member must therefore decide its value for every entry surface at one screen of code.
/// </para>
/// <para>
/// The seam has three surfaces; this type owns the first two and feeds the third:
/// <list type="number">
///   <item><see cref="CompilerOptions"/> construction — the per-surface methods below.</item>
///   <item>The <see cref="CompilerOptions"/>→<see cref="ProjectConfig"/> mapping —
///     <c>SyntheticProject.BuildConfig</c> for the synthetic project-of-one-file.</item>
///   <item>The <see cref="Project.ProjectCompiler"/> flag set — no longer loose constructor
///     parameters; <c>ProjectOptionsMerge.Merge</c> derives the one
///     <c>ProjectCompilerOptions</c> value the compiler takes.</item>
/// </list>
/// </para>
/// <para>
/// <b>Normalization note:</b> <see cref="CompilerOptions.SuppressedWarnings"/> is always built with
/// <see cref="StringComparer.OrdinalIgnoreCase"/> (the comparer <see cref="CompilerOptions"/> itself
/// defaults to). Two CLI project paths previously copied the project's set with
/// <c>new HashSet&lt;string&gt;(other)</c>, which silently drops the source comparer and made a
/// lower-case <c>&lt;NoWarn&gt;spy0451&lt;/NoWarn&gt;</c> fail to suppress <c>SPY0451</c>. The seam
/// only ever widens what a written suppression matches; it cannot stop a working one from matching.
/// </para>
/// </remarks>
public static class CompilerOptionsFactory
{
    /// <summary>
    /// Members of <see cref="CompilerOptions"/> that <see cref="Create"/> deliberately does not
    /// assign, each with the reason it is exempt. Read by the completeness guard, so an exemption is
    /// a reviewed decision rather than an omission. Empty: every member is currently decided here.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> ExemptMembers =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Public <see cref="ProjectConfig"/> members that <see cref="ForProject"/> deliberately does
    /// not read, each with the place it reaches the compiler instead. Read by
    /// <c>ProjectConfigToOptionsConformanceTests</c> (#1554).
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> ProjectConfigExemptMembers =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ProjectFilePath"] =
                "Project identity; not a compiler option. ProjectCompiler reads it directly "
                + "from the config for diagnostic paths and file resolution.",

            ["ProjectDirectory"] =
                "Project identity; not a compiler option. ProjectCompiler reads it directly "
                + "from the config for relative path resolution and output directory computation.",

            ["RootNamespace"] =
                "Read directly from config by ProjectCompiler for namespace assignment; "
                + "ForProject's remarks note that project mode reads this from the config itself, "
                + "so an options-side value would be inert.",

            ["OutputType"] =
                "Read directly from config by ProjectCompiler; ForProject's remarks note "
                + "that project mode reads this from the config itself, so an options-side "
                + "value would be inert.",

            ["TargetFramework"] =
                "Used only by the computed OutputPath getter for output directory layout; "
                + "not a compiler option.",

            ["AssemblyName"] =
                "Read directly from config by ProjectCompiler; ForProject's remarks note "
                + "that project mode reads this from the config itself.",

            ["EntryPoint"] =
                "Read directly from config by ProjectCompiler for entry-point file discovery; "
                + "not a compiler option.",

            ["SourceFiles"] =
                "The compile input file list; read directly from config by ProjectCompiler. "
                + "Not a compiler option — it is the input, not a setting.",

            ["PackageReferences"] =
                "Resolved per-seam by NuGetResolver.ResolvePackage at each of its three "
                + "consumers (Compiler, CompilerApi, AssemblyCompiler) — nothing rewrites "
                + "References upstream, so there is no options-side value to thread.",

            ["ResolvedVersions"] =
                "Populated by the CLI after NuGet restore; threaded to NuGetResolver via "
                + "the config at each resolution site. Not a compiler option (#1580).",

            ["Configuration"] =
                "Read directly from config by ProjectCompiler for output path computation; "
                + "ForProject's remarks note that project mode reads this from the config itself.",

            ["OutputPath"] =
                "Computed getter derived from ProjectDirectory, Configuration, and "
                + "TargetFramework; no independent value to thread.",

            ["OutputAssemblyPathOverride"] =
                "Written by the CLI for single-file mode output path placement; "
                + "read by the computed OutputAssemblyPath getter, not by the options factory.",

            ["UsePrecomputedCodeGenInfo"] =
                "Read directly from config by ProjectCompiler to control CodeGenInfo "
                + "materialization; not a compiler option.",

            ["OutputAssemblyPath"] =
                "Computed virtual getter derived from OutputPath and OutputAssemblyPathOverride; "
                + "no independent value to thread.",
        };

    /// <summary>
    /// Options with no surface-specific opinion: exactly <c>new CompilerOptions()</c>. Used by the
    /// <see cref="Compiler"/> and <see cref="CompilerApi"/> constructors/overloads whose callers
    /// supplied nothing, so the compiler's own defaults (exe output, Debug configuration, no
    /// features) apply.
    /// </summary>
    public static CompilerOptions Default() => Create(new CompilerOptionsSpec());

    /// <summary>
    /// Options for a single-file CLI invocation (<c>build</c>, <c>emit csharp</c>,
    /// <c>emit diagnostics --include-codegen</c>). Flag <i>parsing</i> stays in
    /// <c>System.CommandLine</c> land; this is where the parsed values become options, so the six
    /// hand-assembled per-command initializers (#1061) are one call each.
    /// </summary>
    /// <remarks>
    /// <see cref="CompilerOptions.Incremental"/> is not a parameter: the incremental cache is a
    /// project-mode facility keyed on a project's <c>obj/</c> directory, and a single-file compile
    /// never uses it (the synthetic project-of-one-file is always built with incremental off).
    /// </remarks>
    public static CompilerOptions ForCli(
        string outputType = "exe",
        string[]? references = null,
        string[]? modulePaths = null,
        bool warningsAsErrors = false,
        IEnumerable<string>? suppressedWarnings = null,
        int maxErrors = 0,
        FeatureFlags? features = null,
        string? @namespace = null,
        string configuration = "Debug",
        string? assemblyName = null,
        string? outputAssemblyPath = null)
        => Create(new CompilerOptionsSpec
        {
            OutputType = outputType,
            References = references,
            ModulePaths = modulePaths,
            WarningsAsErrors = warningsAsErrors,
            SuppressedWarnings = suppressedWarnings,
            MaxErrors = maxErrors,
            Features = features,
            Namespace = @namespace,
            Configuration = configuration,
            AssemblyName = assemblyName,
            OutputAssemblyPath = outputAssemblyPath,
        });

    /// <summary>
    /// Options for compiling or building a <c>.spyproj</c>. The project supplies the base
    /// (references, module paths, warnings-as-errors, suppressed warnings) and the CLI flags layer
    /// on top: <paramref name="warningsAsErrors"/> is OR-ed with the project's setting and
    /// <paramref name="additionalSuppressedWarnings"/> (<c>--nowarn</c>) is unioned with
    /// <c>&lt;NoWarn&gt;</c> — the same widening merge <c>ProjectOptionsMerge</c> applies inside the
    /// compiler.
    /// </summary>
    /// <remarks>
    /// <see cref="CompilerOptions.OutputType"/>, <see cref="CompilerOptions.Configuration"/>,
    /// <see cref="CompilerOptions.AssemblyName"/>, <see cref="CompilerOptions.Namespace"/> and
    /// <see cref="CompilerOptions.OutputAssemblyPath"/> are left at their defaults on this surface:
    /// project mode reads all five from the <see cref="ProjectConfig"/> itself, so an options-side
    /// value would be inert (and misleading).
    /// </remarks>
    public static CompilerOptions ForProject(
        ProjectConfig config,
        IEnumerable<string>? defaultReferences = null,
        bool warningsAsErrors = false,
        IEnumerable<string>? additionalSuppressedWarnings = null,
        int maxErrors = 0,
        bool incremental = false,
        FeatureFlags? features = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        var suppressed = new HashSet<string>(config.SuppressedWarnings, StringComparer.OrdinalIgnoreCase);
        if (additionalSuppressedWarnings != null)
            suppressed.UnionWith(additionalSuppressedWarnings);

        var references = (defaultReferences ?? Enumerable.Empty<string>())
            .Concat(config.References)
            .Distinct()
            .ToArray();

        return Create(new CompilerOptionsSpec
        {
            References = references,
            ModulePaths = config.ModulePaths.ToArray(),
            WarningsAsErrors = warningsAsErrors || config.WarningsAsErrors,
            TargetsTestHost = config.TestHost,
            SuppressedWarnings = suppressed,
            MaxErrors = maxErrors,
            Incremental = incremental,
            Features = (features ?? FeatureFlags.None).Enable(config.Features),
        });
    }

    /// <summary>
    /// Options for LSP analysis of an editor buffer. <see cref="CompilerOptions.OutputType"/> is
    /// forced to <c>"library"</c> so in-editor analysis never reports missing-entry-point
    /// diagnostics for a file the user is still typing.
    /// </summary>
    /// <param name="project">
    /// The workspace's <c>.spyproj</c>, when one was discovered. Supplies the base features,
    /// warnings-as-errors, suppressed warnings, references and module paths.
    /// </param>
    /// <param name="features">Features from LSP workspace configuration; unioned over the project's.</param>
    /// <param name="warningsAsErrors">OR-ed with the project's setting.</param>
    /// <param name="suppressedWarnings">Unioned with the project's <c>&lt;NoWarn&gt;</c>.</param>
    /// <param name="maxErrors">
    /// Caller-supplied only — <see cref="ProjectConfig"/> has no counterpart.
    /// </param>
    /// <param name="references">Appended (distinct) after the project's references.</param>
    /// <param name="modulePaths">Appended (distinct) after the project's module paths.</param>
    /// <remarks>
    /// The layering is "widest wins" rather than "last writer wins" for the boolean and set-valued
    /// members: an editor cannot un-suppress a warning the project suppressed, nor disable a feature
    /// the project enabled, because doing so would make in-editor diagnostics diverge from the
    /// build — the exact divergence #1149 reports and the parity sweep gates.
    /// </remarks>
    public static CompilerOptions ForLsp(
        ProjectConfig? project = null,
        FeatureFlags? features = null,
        bool warningsAsErrors = false,
        IEnumerable<string>? suppressedWarnings = null,
        int maxErrors = 0,
        IEnumerable<string>? references = null,
        IEnumerable<string>? modulePaths = null)
    {
        var suppressed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (project != null)
            suppressed.UnionWith(project.SuppressedWarnings);
        if (suppressedWarnings != null)
            suppressed.UnionWith(suppressedWarnings);

        var effectiveFeatures = features ?? FeatureFlags.None;
        if (project != null)
            effectiveFeatures = effectiveFeatures.Enable(project.Features);

        return Create(new CompilerOptionsSpec
        {
            OutputType = "library",
            References = Combine(project?.References, references),
            ModulePaths = Combine(project?.ModulePaths, modulePaths),
            WarningsAsErrors = warningsAsErrors || (project?.WarningsAsErrors ?? false),
            TargetsTestHost = project?.TestHost ?? false,
            SuppressedWarnings = suppressed,
            MaxErrors = maxErrors,
            Features = effectiveFeatures,
        });
    }

    /// <summary>
    /// Options for a REPL submission. The REPL compiles through a bespoke in-memory
    /// <see cref="ProjectConfig"/> (its source is a replayed buffer, not a file), and shapes that
    /// config's shared fields — output type, root namespace, warning settings — from these options,
    /// so the REPL participates in the seam like every other entry point instead of hard-coding a
    /// second set of defaults.
    /// </summary>
    public static CompilerOptions ForRepl(FeatureFlags? features = null)
        => Create(new CompilerOptionsSpec { Features = features });

    /// <summary>
    /// Options for analysis-only consumers that must not require an entry point: the
    /// <see cref="CompilerApi.Analyze(string, CancellationToken)"/> overload that takes no options,
    /// and the web playground's compile preview (which shows generated C# for a snippet that has no
    /// <c>main()</c>).
    /// </summary>
    public static CompilerOptions ForLibraryAnalysis()
        => Create(new CompilerOptionsSpec { OutputType = "library" });

    /// <summary>
    /// The construction core: the one statement in the product that assigns
    /// <see cref="CompilerOptions"/> members. Every member is assigned from an explicit
    /// <see cref="CompilerOptionsSpec"/> field — nothing is left to the property initializers, so
    /// the guard can read this initializer and prove completeness.
    /// </summary>
    private static CompilerOptions Create(CompilerOptionsSpec spec) => new()
    {
        ModulePaths = spec.ModulePaths,
        References = spec.References,
        WarningsAsErrors = spec.WarningsAsErrors,
        TargetsTestHost = spec.TargetsTestHost,
        SuppressedWarnings = spec.SuppressedWarnings is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(spec.SuppressedWarnings, StringComparer.OrdinalIgnoreCase),
        MaxErrors = spec.MaxErrors,
        Incremental = spec.Incremental,
        OutputType = spec.OutputType,
        Namespace = spec.Namespace,
        Features = spec.Features ?? FeatureFlags.None,
        Configuration = spec.Configuration,
        AssemblyName = spec.AssemblyName,
        OutputAssemblyPath = spec.OutputAssemblyPath,
    };

    /// <summary>
    /// Concatenates two optional path/reference sequences, preserving order and dropping duplicates.
    /// Returns null when both are absent so the options keep their "unset" shape.
    /// </summary>
    private static string[]? Combine(IEnumerable<string>? first, IEnumerable<string>? second)
    {
        if (first == null && second == null)
            return null;

        return (first ?? Enumerable.Empty<string>())
            .Concat(second ?? Enumerable.Empty<string>())
            .Distinct()
            .ToArray();
    }
}

/// <summary>
/// The explicit parameter object <see cref="CompilerOptionsFactory.Create"/> reads. It carries one
/// field per <see cref="CompilerOptions"/> member, with the member's own default, so a per-surface
/// method states only what that surface decides differently and the core still assigns everything.
/// </summary>
internal sealed class CompilerOptionsSpec
{
    public string[]? ModulePaths { get; init; }

    public string[]? References { get; init; }

    public bool WarningsAsErrors { get; init; }

    /// <summary>
    /// Whether the emitted C# is compiled into a test host (#1495). Set only from a project's
    /// <c>&lt;TestHost&gt;</c> property — a single-file CLI invocation has no project to declare it
    /// and therefore never targets one.
    /// </summary>
    public bool TargetsTestHost { get; init; }

    public IEnumerable<string>? SuppressedWarnings { get; init; }

    public int MaxErrors { get; init; }

    public bool Incremental { get; init; }

    public string OutputType { get; init; } = "exe";

    public string? Namespace { get; init; }

    public FeatureFlags? Features { get; init; }

    public string Configuration { get; init; } = "Debug";

    public string? AssemblyName { get; init; }

    public string? OutputAssemblyPath { get; init; }
}
