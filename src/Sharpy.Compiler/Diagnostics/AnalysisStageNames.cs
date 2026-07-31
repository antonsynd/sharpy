namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Names for the per-call stages a single-file analysis runs through, used to attribute the LSP's
/// change-to-publish latency (#1140).
/// </summary>
/// <remarks>
/// <para>
/// Distinct from <see cref="CompilerPhaseNames"/>, which names the per-file compilation phases
/// every compile records. These name the <em>per-call</em> whole-project scaffolding that a
/// single-file <c>CompilerApi.Analyze</c> has had to rebuild on every keystroke since #1087 routed
/// it through <c>ProjectCompiler.AnalyzeProject</c> — the cost the recorded baseline reports only
/// in aggregate.
/// </para>
/// <para>
/// Attribution is opt-in: it is collected only when a caller supplies a
/// <see cref="CompilationMetrics"/> to hold it, and with none every bracket is a null check.
/// </para>
/// </remarks>
internal static class AnalysisStageNames
{
    /// <summary>
    /// Building the synthetic single-file <c>ProjectConfig</c>, including the local-import closure
    /// walk and the entry file's pre-parse (the parse-once path, #1095/#1137).
    /// </summary>
    public const string SyntheticProjectSetup = "Synthetic Project Setup";

    /// <summary>
    /// Constructing the <c>ModuleRegistry</c> and loading every default/project reference. Zero when
    /// there are no references at all — the short circuit that made this cost invisible to the
    /// recorded baseline.
    /// </summary>
    public const string ModuleRegistry = "Module Registry";

    /// <summary>Constructing the fresh per-call <c>ProjectModel</c>, diagnostic bag, and metrics.</summary>
    public const string ProjectSetup = "Project Setup";

    /// <summary>The project pipeline's parse phase (consumes pre-parsed units when present).</summary>
    public const string ProjectParse = "Project Parse";

    /// <summary>Initializing the shared symbol table and semantic info.</summary>
    public const string SharedStateInit = "Shared State Init";

    /// <summary>Collecting type declarations across the project (type shells only).</summary>
    public const string TypeDeclarations = "Type Declarations";

    /// <summary>
    /// Resolving inheritance, including <c>FileCompilationPipeline</c>'s imported-inheritance pass.
    /// </summary>
    public const string InheritanceResolution = "Inheritance Resolution";

    /// <summary>Per-file semantic analysis (type resolution, type checking, validation).</summary>
    public const string SemanticAnalysis = "Semantic Analysis";

    /// <summary>Freezing computed semantic data onto symbols (<c>MaterializeTypeInfo</c>).</summary>
    public const string Materialization = "Materialization";

    /// <summary>Projecting the entry file's per-unit view out of the project analysis.</summary>
    public const string EntryFileResult = "Entry File Result";
}
