using System.Collections.Concurrent;
using System.Text.Json;
// The enclosing `Sharpy` namespace exposes Sharpy.List<T> (Core's Pythonic list), which shadows
// System's List<T> here; SCG-qualify the handful of concrete lists this harness constructs.
using SCG = System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using LspModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Utilities;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// Front-end diagnostic-parity sweep (Class F — cross-entry-point drift). Umbrella contract: #1144.
///
/// <para>
/// Sharpy has multiple compiler front doors that each hand-thread options and drive a phase
/// pipeline. Historically they diverge one dimension at a time — the REPL rendered raw
/// <c>CSxxxx</c> to users (#1059), <c>emit csharp</c> dropped <c>--enable-feature</c> so gated
/// syntax hit SPY0331 (#1097), and LSP <c>.spyproj</c> analysis constructed its
/// <see cref="ProjectCompiler"/> without warnings/maxErrors/features (#1109). There was NO test
/// asserting the entry points agree on diagnostics for the same source. This sweep is that test.
/// </para>
///
/// <para>
/// <b>Contract.</b> The same fixture source, presented with <i>equivalent</i> options to each front
/// end, must yield the same <b>in-scope diagnostic-code multiset</b> as the
/// <see cref="CompilerApi.Analyze"/> baseline, after the documented normalization below. Anything
/// that differs and is not explicitly justified is a parity violation.
/// </para>
///
/// <para>
/// <b>Entry points compared (each pairwise against the Analyze baseline):</b>
/// <list type="number">
///   <item><c>compile</c> — <see cref="CompilerApi.Compile"/> (the CLI compile path).</item>
///   <item><c>LSP</c> — <see cref="SharpyWorkspace"/> <c>OpenDocument</c> + <c>GetAnalysisAsync</c>.</item>
///   <item><c>REPL</c> — <see cref="ReplSession"/>'s real front door (classification + replay + compile),
///     via a non-executing probe.</item>
///   <item><c>compile-project</c> — <see cref="ProjectCompiler.Compile"/> vs
///     <see cref="CompilerApi.AnalyzeProject"/>, for multi-file fixtures only (LSP/REPL are
///     single-file surfaces).</item>
///   <item><c>emit-diagnostics</c> — the seam <c>sharpyc emit diagnostics</c> calls:
///     <see cref="CompilerApi.Analyze(string, string, CompilerOptions, CancellationToken)"/>,
///     analysis of an entry file named BY PATH. Added with #1377, where this door could not
///     resolve sibling imports and therefore reported diagnostics for a different program than
///     <c>run</c>. Compared on both fixture arities: against the Analyze baseline for single-file
///     fixtures, and against the AnalyzeProject baseline for multi-file ones — the latter is the
///     cell that would have caught #1377.</item>
///   <item><c>LSP-project</c> — the editor in PROJECT mode (#2242): the multi-file fixture copied to
///     a temp root under a generated <c>project.spyproj</c> listing every file,
///     <see cref="LanguageService.InitializeProjectAsync"/>, then
///     <see cref="LanguageService.GetAnalysisAsync"/> per file — the per-file view every handler and
///     the diagnostic publisher read. Two checks under one key: (i) the union of the per-file
///     in-scope multisets equals the AnalyzeProject baseline (a build diagnostic that lands in no
///     file's result is a missing entry, so the oracle iterates the project files AND every distinct
///     <c>FilePath</c> the project bag stamps); (ii) attribution — every diagnostic in file F's
///     result is stamped F, or is unstamped and F is the unattributed-diagnostic target (the
///     project's <c>EntryPoint</c>, else its first source file, plan-6ce1a5 D5). A diagnostic on
///     the wrong file is a violation even when the union matches; it is recorded as
///     <c>misattributed:&lt;code&gt;@&lt;file&gt;</c> in the comparison's added list.</item>
///   <item><c>LSP-project-nav</c> — go-to-definition on every imported spelling in the same loaded
///     project (#2242): the first use of every <c>from m import x</c> name, and every
///     <c>MemberAccess</c> whose receiver binds a module (<c>m.x</c>, <c>a.b.x</c>, an
///     <c>import m as n</c> alias), asked of <see cref="Sharpy.Lsp.Handlers.SharpyDefinitionHandler"/> at the
///     name's token. The oracle is the fixture's own module files: the answer must land in the
///     module's file (or, for a re-exported name, in the file declaring the export). Modules with
///     no file (.NET namespaces, the stdlib) are record-and-skipped with the reason, counted in the
///     report. One allowlist key per cell: <c>LSP-project-nav::&lt;fixture&gt;::&lt;file&gt;:&lt;line&gt;:&lt;col&gt;</c>.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Normalization (audit this table against real bugs — none of it may paper over drift):</b>
/// <list type="table">
///   <item><term>N1 — in-scope filter (all pairs)</term><description>The signature is the multiset
///     of diagnostic <c>Code</c>s with severity Error or Warning whose <c>SPYnnnn</c> number is in
///     [0001, 0499] — the Lexer, Parser, Semantic, and Validation phases. Code generation
///     (SPY05xx), infrastructure (SPY09xx), info notes (SPY1xxx, e.g. the SPY1001 implicit-interface
///     synthesis note the emitter emits), and uncoded wrapper diagnostics are excluded, because
///     Analyze and LSP never run code generation, so those are not producible by every entry point.
///     Two exceptions are admitted by code, not by band — SPY0522 and SPY0523, which are numbered in
///     the code-generation range but emitted during semantic analysis/validation (see
///     <c>FrontEndEmittedCollisionCodes</c>); without them every name-collision fixture compared an
///     empty multiset. Real code-generation leaks (X1 / SPY0908) are guarded by ReparseEquivalence +
///     executing fixtures, not here.</description></item>
///   <item><term>N2 — OutputType held constant (compile)</term><description><see cref="CompilerApi.Compile"/>
///     is invoked with <c>OutputType=library</c> to match the Analyze baseline. The only
///     library-vs-exe front-end diagnostic is SPY0403 (MissingMainFunction), gated purely by
///     OutputType, not pipeline identity — so we hold OutputType constant rather than let it inject
///     a false diff. Exe-mode entry-point behavior is instead exercised by the REPL (which forces
///     exe and always synthesizes a <c>main()</c>).</description></item>
///   <item><term>N3 — REPL classification skip</term><description>The REPL only compares when its own
///     <see cref="ReplSession.ClassifyInput"/> classifies the whole fixture as a module-level
///     submission. Inputs it classifies as executable (wrapped in a synthesized <c>main()</c>) or
///     rejects at pre-parse are a different program shape; they are record-and-skipped with the
///     classification recorded in the report.</description></item>
///   <item><term>N4 — REPL main() collision skip</term><description>The REPL always appends a
///     synthesized <c>def main()</c>. A fixture that defines its own top-level <c>main</c> would
///     collide (duplicate definition), so such fixtures are record-and-skipped for the REPL. This is
///     an intrinsic property of the REPL's whole-module replay, not a pipeline bug.</description></item>
/// </list>
/// After N1–N4 no per-code allowlisting is expected: for eligible fixtures every front end should
/// agree exactly. Residual divergences are real drift and either fail the ratchet or (pending
/// triage) get one reviewed line in <c>frontend-parity-allowlist.txt</c>.
/// </para>
///
/// <para>
/// <b>Deleted rule.</b> N5 used to record-and-skip <c>.features</c> fixtures for the LSP pair,
/// because <see cref="SharpyWorkspace"/> hardcoded its analysis options and could not source
/// workspace features. Fixing that (#1149) made the skip unnecessary, and deleting it was the
/// issue's acceptance criterion: the LSP now compares <c>.features</c> fixtures like every other
/// entry point, so a future workspace that stops threading features fails this sweep instead of
/// reaching a user as a red squiggle on code the CLI compiles.
/// </para>
///
/// <para>
/// This is a <b>sweep</b> (Category=GapDiscovery), traited out of the fast suite. It aggregates one
/// JSON report under <c>.claude/tmp/</c>, runs entirely in-process (no subprocess, no fixture
/// execution — the REPL is probed for diagnostics only), and hard-fails on any crash or any
/// non-allowlisted violation once the reviewed allowlist file exists (ratchet mode).
/// </para>
/// </summary>
[Trait("Category", "GapDiscovery")]
public class FrontEndParityTests
{
    private const string EntryCompile = "compile";
    private const string EntryLsp = "LSP";
    private const string EntryRepl = "REPL";
    private const string EntryCompileProject = "compile-project";
    private const string EntryEmitDiagnostics = "emit-diagnostics";
    private const string EntryLspProject = "LSP-project";
    private const string EntryLspProjectNav = "LSP-project-nav";

    // Diagnostics whose SPYnnnn number is in [1, 499] are front-end (Lexer/Parser/Semantic/
    // Validation). 500-599 = code generation, 900-999 = infrastructure, 1000+ = info notes — all
    // out of scope per N1, except the two collision codes listed in FrontEndEmittedCollisionCodes,
    // which are numbered in the 5xx band but emitted before code generation.
    private static readonly Regex SpyCode = new(@"^SPY(\d{4})$", RegexOptions.Compiled);

    private readonly ITestOutputHelper _output;

    public FrontEndParityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task FrontEndParity_AllFixtures_AgreeWithAnalyzeBaseline()
    {
        var (corePath, stdlibPath) = StdlibReferences.Resolve();
        Assert.True(File.Exists(corePath), $"Sharpy.Core.dll not found at {corePath}");
        Assert.True(File.Exists(stdlibPath), $"Sharpy.Stdlib.dll not found at {stdlibPath}");

        // One CompilerApi carrying the same Core+Stdlib references the REPL loads internally, so
        // stdlib imports resolve identically across every single-file entry point (otherwise a
        // reference-config gap would masquerade as a pipeline divergence).
        var references = new[] { corePath, stdlibPath };
        var api = new CompilerApi(Sharpy.Compiler.Logging.NullLogger.Instance, references);

        var fixtures = FixtureDiscoveryHelper.DiscoverFixtures(ResolveFixturesPath()).ToList();
        Assert.True(fixtures.Count > 0, "No fixtures discovered — corpus path is wrong.");

        // Deterministic sub-sampling keeps the sweep tractable while always covering the drift-prone
        // surface: every fixture whose path mentions repl/emit/feature/experimental and every
        // .features fixture is included regardless of stride.
        var stride = Math.Max(1, ReadIntEnv("FRONTEND_PARITY_STRIDE", 1));
        var sampled = fixtures.Where(f => IsAlwaysIncluded(f) || StableBucket(f.TestName, stride) == 0).ToList();

        var comparisons = new ConcurrentBag<Comparison>();
        var crashes = new ConcurrentBag<CrashRecord>();
        var skips = new ConcurrentBag<SkipRecord>();
        var projectStats = new ProjectModeStats();

        var dop = Math.Max(1, ReadIntEnv("FRONTEND_PARITY_DOP", Math.Min(8, Environment.ProcessorCount)));
        await Parallel.ForEachAsync(
            sampled,
            new ParallelOptions { MaxDegreeOfParallelism = dop },
            async (fixture, ct) =>
            {
                try
                {
                    if (fixture.IsMultiFile)
                        await SweepMultiFileAsync(api, references, fixture, comparisons, crashes, projectStats, ct)
                            .ConfigureAwait(false);
                    else
                        await SweepSingleFileAsync(api, fixture, comparisons, crashes, skips, ct)
                            .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    crashes.Add(new CrashRecord(fixture.TestName, "harness", $"{ex.GetType().Name}: {ex.Message}"));
                }
            });

        var violations = comparisons.Where(c => !c.Match).ToList();
        var matches = comparisons.Count(c => c.Match);

        var allowlist = LoadAllowlist();
        var offenders = violations.Where(v => !allowlist.Matches(v.Key)).ToList();
        var stale = comparisons
            .Where(c => c.Match && allowlist.ExactKeys.Contains(c.Key))
            .Select(c => c.Key)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        var multiFileSwept = sampled.Count(f => f.IsMultiFile);
        WriteReport(fixtures.Count, sampled.Count, comparisons, violations, crashes.ToList(),
            skips.ToList(), allowlist, offenders.Count, stale, multiFileSwept, projectStats);

        _output.WriteLine($"Fixtures discovered: {fixtures.Count}  swept: {sampled.Count}  (stride {stride})  multi-file: {multiFileSwept}");
        _output.WriteLine($"Comparisons: {comparisons.Count}  Matches: {matches}  Violations: {violations.Count}");
        _output.WriteLine($"Justified-normalized skips: {skips.Count}  Crashes: {crashes.Count}");
        _output.WriteLine($"Allowlist size: {allowlist.Count}  Non-allowlisted violations: {offenders.Count}  Stale allowlist entries: {stale.Count}");
        _output.WriteLine(
            $"LSP-project: compared {comparisons.Count(c => c.EntryPoint == EntryLspProject)}  " +
            $"attributed diagnostics {projectStats.AttributedDiagnostics} (non-entry file {projectStats.AttributedNonEntryDiagnostics})  " +
            $"unstamped project-bag diagnostics {projectStats.Unstamped.Count}");
        _output.WriteLine(
            $"LSP-project-nav: compared {comparisons.Count(c => c.EntryPoint == EntryLspProjectNav)}  " +
            $"from cells {projectStats.NavigationCellCount("from")}  member cells {projectStats.NavigationCellCount("member")}  " +
            $"skipped {projectStats.NavigationSkips.Values.Sum()}");

        Assert.True(comparisons.Count > 0, "Front-end parity sweep produced zero comparisons.");

        // Vacuity control for the LSP-project attribution oracle (#2242): the type-check phase writes
        // both bags with the file's stamp, so a corpus whose files carry type errors must show
        // correctly attributed diagnostics. Zero means the oracle never saw a stamped diagnostic —
        // it would pass any attribution, right or wrong.
        if (multiFileSwept > 0)
        {
            Assert.True(projectStats.AttributedDiagnostics > 0,
                "LSP-project attribution oracle is vacuous: no per-file result carried a diagnostic " +
                "stamped with that file across the multi-file corpus.");

            // Vacuity controls for the navigation sweep: both spellings must be present in the
            // corpus, or the sweep compares nothing for the spelling it exists to watch.
            Assert.True(projectStats.NavigationCellCount("from") > 0,
                "LSP-project-nav found no `from m import x` use to ask about — the sweep is vacuous for the bare spelling.");
            Assert.True(projectStats.NavigationCellCount("member") > 0,
                "LSP-project-nav found no module-qualified `m.x` access to ask about — the sweep is vacuous for the qualified spelling.");
        }

        // A crash in any front end on any fixture is always a hard failure.
        Assert.True(crashes.IsEmpty,
            "Front-end parity sweep hit crashes (an entry point threw on a fixture):\n" +
            string.Join("\n", crashes.Take(25).Select(c => $"  {c.Fixture} [{c.EntryPoint}] {c.Exception}")) +
            "\nFull list: .claude/tmp/frontend-parity-report.json");

        // Ratchet: once the reviewed allowlist file exists, any non-allowlisted violation fails CI.
        if (AllowlistFileExists())
        {
            Assert.True(offenders.Count == 0,
                "Front-end parity ratchet: these (entry-point, fixture) pairs disagree with the " +
                "Analyze baseline on the in-scope diagnostic multiset and are not on the reviewed " +
                "allowlist. Fix the entry point / file an issue, or add a justified allowlist entry.\n" +
                string.Join("\n", offenders.Take(50).Select(o =>
                    $"  {o.Key}  baseline=[{string.Join(",", o.BaselineCodes)}] {o.EntryPoint}=[{string.Join(",", o.EntryCodes)}]" +
                    $" (+[{string.Join(",", o.Added)}] -[{string.Join(",", o.Removed)}])")) +
                "\nFull list: .claude/tmp/frontend-parity-report.json");

            Assert.True(stale.Count == 0,
                $"{stale.Count} allowlist entr{(stale.Count == 1 ? "y" : "ies")} no longer " +
                "diverge — whatever fixed them must also delete the entries " +
                "(docs/design/gap-discovery-contracts.md). Delete these lines from " +
                "Conformance/frontend-parity-allowlist.txt:\n  " +
                string.Join("\n  ", stale));
        }
    }

    // ---- single-file sweep: compile, LSP, REPL vs Analyze ----

    private async Task SweepSingleFileAsync(
        CompilerApi api, TestFixtureInfo fixture,
        ConcurrentBag<Comparison> comparisons, ConcurrentBag<CrashRecord> crashes,
        ConcurrentBag<SkipRecord> skips, CancellationToken ct)
    {
        var source = File.ReadAllText(fixture.SpyFilePath);
        var features = FeatureFlags.None.Enable(fixture.Features);

        // Baseline: analysis of the fixture AS THE FILE IT IS (library mode), features threaded so
        // gated fixtures match. The baseline names the file for the same reason every real front
        // door does — a .spy file on disk has a name, and two diagnostic classes are derived from
        // it (SPY0523 members-class collision, SPY0302 self-import). A nameless baseline could not
        // produce either, so it disagreed with `run`, `project`, `emit diagnostics`, the LSP, and
        // the fixtures' own .error sidecars, and the disagreement had to be carried on the
        // allowlist as the baseline's own limitation (#1433). AnalyzeDocument is the shape that
        // names the file while keeping the #1087 symbol-nullification the LSP arm relies on, so
        // the baseline and the editor describe the same program.
        SemanticResult baseline;
        try
        {
            baseline = api.AnalyzeDocument(source, fixture.SpyFilePath,
                new CompilerOptions { OutputType = "library", Features = features }, null, ct);
        }
        catch (Exception ex)
        {
            crashes.Add(new CrashRecord(fixture.TestName, "Analyze(baseline)", $"{ex.GetType().Name}: {ex.Message}"));
            return;
        }
        var baselineSig = Signature(baseline.Diagnostics);

        // --- compile (N2: library mode to match the baseline OutputType) ---
        try
        {
            // Named by path, as CompileCommand names its input file. A null path here made this
            // arm model a door that does not exist (#1433).
            var compile = api.Compile(source, new CompilerOptions { OutputType = "library", Features = features },
                fixture.SpyFilePath, ct);
            comparisons.Add(Compare(EntryCompile, fixture.TestName, baselineSig, Signature(compile.Diagnostics)));
        }
        catch (Exception ex)
        {
            crashes.Add(new CrashRecord(fixture.TestName, EntryCompile, $"{ex.GetType().Name}: {ex.Message}"));
        }

        // --- emit diagnostics (analysis of an entry file named BY PATH, #1377) ---
        // Exercised through the very seam the CLI command calls, in-process, per this sweep's
        // no-subprocess rule. Options are identical to the baseline's, so the single variable
        // under test is that this door is told where the file lives. For a single-file fixture
        // that is a program of one file either way — the cell exists so the path-carrying
        // overload cannot regress single-file analysis while fixing the multi-file case.
        try
        {
            var emit = api.Analyze(source, fixture.SpyFilePath,
                new CompilerOptions { OutputType = "library", Features = features }, ct);
            comparisons.Add(Compare(EntryEmitDiagnostics, fixture.TestName, baselineSig, Signature(emit.Diagnostics)));
        }
        catch (Exception ex)
        {
            crashes.Add(new CrashRecord(fixture.TestName, EntryEmitDiagnostics, $"{ex.GetType().Name}: {ex.Message}"));
        }

        // --- LSP (features threaded through the workspace's own configuration source, #1149) ---
        try
        {
            using var workspace = new SharpyWorkspace(api, NullLogger<SharpyWorkspace>.Instance);
            // The editor's equivalent of the `Features = features` the other entry points pass:
            // `sharpy.features` workspace configuration, which the workspace resolves into its
            // analysis options through the same CompilerOptionsFactory seam.
            workspace.SetConfiguredFeatures(fixture.Features);
            // The document is opened at its REAL path, which is what an editor does. The former
            // synthetic "file:///parity/<escaped-test-name>.spy" only accidentally round-tripped to
            // the right basename (the escaped separator un-escapes in LocalPath) and rooted the
            // import closure at a directory that does not exist (#1433).
            var uri = new Uri(fixture.SpyFilePath).ToString();
            workspace.OpenDocument(uri, source, 1);
            var lsp = await workspace.GetAnalysisAsync(uri, ct).ConfigureAwait(false);
            if (lsp != null)
                comparisons.Add(Compare(EntryLsp, fixture.TestName, baselineSig, Signature(lsp.Diagnostics)));
            else
                crashes.Add(new CrashRecord(fixture.TestName, EntryLsp, "GetAnalysisAsync returned null for an open document"));
        }
        catch (Exception ex)
        {
            crashes.Add(new CrashRecord(fixture.TestName, EntryLsp, $"{ex.GetType().Name}: {ex.Message}"));
        }

        // --- REPL (N3 classification skip, N4 main-collision skip) ---
        if (DefinesTopLevelMain(baseline.Ast))
        {
            skips.Add(new SkipRecord(fixture.TestName, EntryRepl,
                "N4: fixture defines main(); REPL appends a synthesized main() (collision)"));
        }
        else
        {
            try
            {
                var probe = new ReplSession(Sharpy.Compiler.Logging.NullLogger.Instance, features)
                    .ProbeFrontEndDiagnostics(source, ct);
                if (probe.Classification == ReplSession.ReplInputClassification.ModuleLevel)
                    comparisons.Add(Compare(EntryRepl, fixture.TestName, baselineSig, Signature(probe.Diagnostics)));
                else
                    skips.Add(new SkipRecord(fixture.TestName, EntryRepl,
                        $"N3: REPL classified input as {probe.Classification} (not a module-level submission)"));
            }
            catch (Exception ex)
            {
                crashes.Add(new CrashRecord(fixture.TestName, EntryRepl, $"{ex.GetType().Name}: {ex.Message}"));
            }
        }
    }

    // ---- multi-file sweep: ProjectCompiler.Compile vs CompilerApi.AnalyzeProject ----

    private static async Task SweepMultiFileAsync(
        CompilerApi api, string[] references, TestFixtureInfo fixture,
        ConcurrentBag<Comparison> comparisons, ConcurrentBag<CrashRecord> crashes,
        ProjectModeStats projectStats, CancellationToken ct)
    {
        var projectDir = fixture.SpyFilePath;
        var spyFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.AllDirectories)
            .Where(f => !CrashBundleWriter.IsNonSourceSegment(Path.GetRelativePath(projectDir, f)))
            .ToList();
        var mainSpy = Path.Combine(projectDir, "main.spy");
        var entryPoint = File.Exists(mainSpy) ? "main.spy" : Path.GetFileName(spyFiles[0]);
        var features = FeatureFlags.None.Enable(fixture.Features);

        // A single config drives both sides, so OutputType/entry-point rules are held constant by
        // construction — the comparison isolates the AnalyzeProject-vs-Compile method drift (#1109).
        ProjectConfig MakeConfig() => new()
        {
            ProjectDirectory = projectDir,
            ProjectFilePath = Path.Combine(projectDir, "project.spyproj"),
            RootNamespace = "Test",
            OutputType = "exe",
            EntryPoint = entryPoint,
            SourceFiles = new SCG.List<string>(spyFiles),
        };

        var options = new CompilerOptions { Features = features };

        IReadOnlyList<CompilerDiagnostic> baselineDiags;
        try
        {
            baselineDiags = api.AnalyzeProject(MakeConfig(), options).Diagnostics.GetAll();
        }
        catch (Exception ex)
        {
            crashes.Add(new CrashRecord(fixture.TestName, "AnalyzeProject(baseline)", $"{ex.GetType().Name}: {ex.Message}"));
            return;
        }

        try
        {
            var registry = BuildProjectRegistry(references, MakeConfig());
            var compiler = new ProjectCompiler(
                Sharpy.Compiler.Logging.NullLogger.Instance, registry,
                ProjectCompilerOptions.Default with { MaxErrors = options.MaxErrors, Features = features },
                new RoslynEmitterFactory());
            var result = compiler.Compile(MakeConfig(), CancellationToken.None, emitAssembly: false);
            comparisons.Add(Compare(EntryCompileProject, fixture.TestName,
                Signature(baselineDiags), Signature(result.Diagnostics.GetAll())));
        }
        catch (Exception ex)
        {
            crashes.Add(new CrashRecord(fixture.TestName, EntryCompileProject, $"{ex.GetType().Name}: {ex.Message}"));
        }

        // --- emit diagnostics: THE #1377 cell ---
        // `sharpyc emit diagnostics main.spy` is handed one file, not a project. Everything else
        // about the program — the sibling modules, their symbols, the diagnostics they carry —
        // has to be discovered from that file's directory, which is exactly what this door failed
        // to do: it reported SPY0300 for every sibling import and then described whatever program
        // the unresolved names left behind. The AnalyzeProject baseline above is the program that
        // actually exists, so this comparison is the standing statement that one file's worth of
        // input still buys the whole program's diagnostics.
        //
        // N2 applies: OutputType is held at the baseline's "exe" so a missing-main difference
        // cannot masquerade as closure drift.
        try
        {
            var entryFile = Path.Combine(projectDir, entryPoint);
            var emit = api.Analyze(File.ReadAllText(entryFile), entryFile,
                new CompilerOptions { OutputType = "exe", Features = features });
            comparisons.Add(Compare(EntryEmitDiagnostics, fixture.TestName,
                Signature(baselineDiags), Signature(emit.Diagnostics)));
        }
        catch (Exception ex)
        {
            crashes.Add(new CrashRecord(fixture.TestName, EntryEmitDiagnostics, $"{ex.GetType().Name}: {ex.Message}"));
        }

        // --- LSP-project: the editor's per-file view in project mode (#2242) ---
        try
        {
            await SweepLspProjectAsync(api, fixture, projectDir, spyFiles, entryPoint,
                Signature(baselineDiags), comparisons, crashes, projectStats, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            crashes.Add(new CrashRecord(fixture.TestName, EntryLspProject, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <summary>
    /// The <c>LSP-project</c> pair (#2242, plan-6ce1a5 Phase 1): the editor's per-file results in
    /// project mode, read through <see cref="LanguageService.GetAnalysisAsync"/> — the seam the
    /// diagnostic publisher and every handler read — against the build's bag.
    /// </summary>
    /// <remarks>
    /// The fixture tree is shared read-only state, and <see cref="LanguageService.InitializeProjectAsync"/>
    /// discovers its <c>.spyproj</c> in the workspace root, so the fixture is copied to a temp root and
    /// a <c>project.spyproj</c> listing the same files as <c>MakeConfig</c> is written there. The
    /// fixture's features travel in <c>&lt;Features&gt;</c>, the way a real project carries them;
    /// without them every gated fixture would be an SPY0331 red that is not this class.
    /// </remarks>
    private static async Task SweepLspProjectAsync(
        CompilerApi api, TestFixtureInfo fixture, string projectDir, IReadOnlyList<string> spyFiles,
        string entryPoint, IReadOnlyList<string> baselineSig,
        ConcurrentBag<Comparison> comparisons, ConcurrentBag<CrashRecord> crashes,
        ProjectModeStats stats, CancellationToken ct)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"sharpy_parity_project_{Guid.NewGuid():N}");
        try
        {
            CopyFixtureTree(projectDir, tempRoot);
            WriteProjectFile(tempRoot,
                spyFiles.Select(f => Path.GetRelativePath(projectDir, f)), entryPoint, fixture.Features);

            using var workspace = new SharpyWorkspace(api, NullLogger<SharpyWorkspace>.Instance);
            using var service = new LanguageService(workspace, api, NullLogger<LanguageService>.Instance);
            await service.InitializeProjectAsync(tempRoot, ct).ConfigureAwait(false);

            var analysis = service.ProjectAnalysis;
            if (!service.HasProject || analysis == null)
            {
                crashes.Add(new CrashRecord(fixture.TestName, EntryLspProject,
                    "InitializeProjectAsync loaded no project from the generated project.spyproj"));
                return;
            }

            var config = analysis.ProjectModel.Config;
            var unattributedTarget = PathNormalizer.Normalize(UnattributedTargetFile(config));
            var entryFile = config.EntryPoint != null
                ? PathNormalizer.Normalize(Path.Combine(config.ProjectDirectory, config.EntryPoint))
                : null;
            var bag = analysis.Diagnostics.GetAll();

            // The files the oracle reads: every project file, plus every distinct path the project bag
            // stamps (plan-6ce1a5 [ADDED: adequacy]) — an entry stamped with a non-project path lands in
            // no project file's result, and must surface as a missing entry, not vanish.
            var filesToRead = new SCG.Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var uri in service.GetProjectFileUris())
            {
                var path = new Uri(uri).LocalPath;
                filesToRead.TryAdd(PathNormalizer.Normalize(path), uri);
            }
            foreach (var d in bag)
            {
                if (string.IsNullOrEmpty(d.FilePath))
                {
                    stats.Unstamped.Add(new UnstampedRecord(fixture.TestName, d.Code ?? "<uncoded>", d.Message));
                    continue;
                }
                if (Path.IsPathRooted(d.FilePath))
                    filesToRead.TryAdd(PathNormalizer.Normalize(d.FilePath), new Uri(Path.GetFullPath(d.FilePath)).ToString());
            }

            var union = new SCG.List<CompilerDiagnostic>();
            var misattributed = new SCG.SortedSet<string>(StringComparer.Ordinal);
            foreach (var (normalizedFile, uri) in filesToRead.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var result = await service.GetAnalysisAsync(uri, ct).ConfigureAwait(false);
                if (result == null)
                    continue;
                var relative = RelativeToRoot(tempRoot, new Uri(uri).LocalPath);
                foreach (var d in result.Diagnostics)
                {
                    union.Add(d);
                    if (string.IsNullOrEmpty(d.FilePath))
                    {
                        if (normalizedFile != unattributedTarget)
                            misattributed.Add($"misattributed:{d.Code ?? "<uncoded>"}@{relative}");
                    }
                    else if (PathNormalizer.Normalize(d.FilePath) != normalizedFile)
                    {
                        misattributed.Add($"misattributed:{d.Code ?? "<uncoded>"}@{relative}");
                    }
                    else
                    {
                        stats.CountAttributed(nonEntry: normalizedFile != entryFile);
                    }
                }
            }

            var unionSig = Signature(union);
            var (added, removed) = MultisetDiff(baselineSig, unionSig);
            added.AddRange(misattributed);
            comparisons.Add(new Comparison(EntryLspProject, fixture.TestName, baselineSig, unionSig,
                added, removed, Match: added.Count == 0 && removed.Count == 0));

            // --- LSP-project-nav: definition on every imported spelling, same loaded project ---
            await SweepProjectNavigationAsync(api, service, analysis, fixture, tempRoot, comparisons, stats, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup of the temp copy.
            }
        }
    }

    /// <summary>
    /// The <c>LSP-project-nav</c> pair (#2242, plan-6ce1a5 Phase 1 Task 2): go-to-definition on every
    /// imported spelling the corpus writes, in the loaded project.
    /// </summary>
    /// <remarks>
    /// Two cell kinds, both asked at a name token (1-based compiler position → 0-based LSP position):
    /// <list type="bullet">
    ///   <item><c>from</c> — the first identifier use of each <c>from m import x [as y]</c> binding;
    ///     the answer must land in <c>m</c>'s file (or <c>m.x</c>'s, when <c>x</c> is a submodule).
    ///     The module is found among the project's own units by dotted path — no symbol the handler
    ///     reads is the oracle. These cells navigate at HEAD: they are the positive control that the
    ///     oracle can pass.</item>
    ///   <item><c>member</c> — every <c>MemberAccess</c> whose receiver binds a
    ///     <see cref="Sharpy.Compiler.Semantic.ModuleSymbol"/> (an identifier bound to a module, or a
    ///     nested <c>a.b</c> receiver), asked at <see cref="MemberAccess.MemberNameLineStart"/> /
    ///     <see cref="MemberAccess.MemberNameColumnStart"/>; the answer must land in the module's
    ///     <c>FilePath</c>, or in the declaring file of the export when the module re-exports it.</item>
    /// </list>
    /// A module with no file (a .NET namespace, a stdlib module) has no location to land on: its
    /// cells are record-and-skipped with the reason, counted under <c>projectMode.navigation</c> in
    /// the report, never silently passed.
    /// </remarks>
    private static async Task SweepProjectNavigationAsync(
        CompilerApi api, LanguageService service, ProjectAnalysisResult analysis, TestFixtureInfo fixture,
        string tempRoot, ConcurrentBag<Comparison> comparisons, ProjectModeStats stats, CancellationToken ct)
    {
        const string PackageInit = ".__init__";
        var unitsByModule = new SCG.Dictionary<string, string>(StringComparer.Ordinal);
        var unitsByFile = new SCG.Dictionary<string, Module?>(StringComparer.Ordinal);
        foreach (var unit in analysis.ProjectModel.Units.Values)
        {
            unitsByFile.TryAdd(unit.FilePath, unit.Ast);
            unitsByModule.TryAdd(unit.ModulePath, unit.FilePath);
            if (unit.ModulePath.EndsWith(PackageInit, StringComparison.Ordinal))
                unitsByModule.TryAdd(unit.ModulePath[..^PackageInit.Length], unit.FilePath);
        }

        var handler = new Sharpy.Lsp.Handlers.SharpyDefinitionHandler(service, api);

        foreach (var uri in service.GetProjectFileUris())
        {
            var result = await service.GetAnalysisAsync(uri, ct).ConfigureAwait(false);
            var query = result?.SemanticQuery;
            if (result?.Ast == null || query == null)
            {
                stats.SkipNavigation("file has no analyzed AST in the project result (nothing to ask)");
                continue;
            }

            var fileRel = RelativeToRoot(tempRoot, new Uri(uri).LocalPath);
            var nodes = EnumerateNodes(result.Ast).ToList();
            var identifiers = nodes.OfType<Identifier>()
                .Where(id => id.LineStart > 0)
                .OrderBy(id => id.LineStart).ThenBy(id => id.ColumnStart)
                .ToList();

            var topLevelNames = new SCG.HashSet<string>(
                result.Ast.Body.Select(TopLevelDefinitionName).OfType<string>(), StringComparer.Ordinal);

            var cells = new SCG.List<NavCell>();
            foreach (var fromImport in nodes.OfType<FromImportStatement>())
            {
                if (fromImport.ImportAll)
                {
                    stats.SkipNavigation("from m import *: no single name to ask about");
                    continue;
                }
                if (fromImport.Module.StartsWith('.'))
                {
                    stats.SkipNavigation("relative from-import (resolved by package; none in the corpus when written)");
                    continue;
                }
                var moduleName = fromImport.ModuleParts.IsDefaultOrEmpty
                    ? fromImport.Module
                    : string.Join(".", fromImport.ModuleParts);
                foreach (var alias in fromImport.Names)
                {
                    var local = alias.AsName ?? alias.Name;
                    var targets = new SCG.List<string>();
                    CollectFromImportTargets(moduleName, alias.Name, unitsByModule, unitsByFile, targets, depth: 0);
                    if (targets.Count == 0)
                    {
                        stats.SkipNavigation("from-import of a module with no project file (.NET namespace or stdlib)");
                        continue;
                    }
                    if (topLevelNames.Contains(local))
                    {
                        stats.SkipNavigation("a definition in the importing file shadows the from-imported name");
                        continue;
                    }
                    var use = identifiers.FirstOrDefault(id => string.Equals(id.Name, local, StringComparison.Ordinal));
                    if (use == null)
                    {
                        stats.SkipNavigation("from-imported name is never used as an identifier in the file");
                        continue;
                    }
                    cells.Add(new NavCell("from", local, use.LineStart, use.ColumnStart, targets));
                }
            }

            foreach (var access in nodes.OfType<MemberAccess>())
            {
                var module = ResolveModuleReceiver(access.Object, query);
                if (module == null)
                    continue;
                if (module.IsNetModule || string.IsNullOrEmpty(module.FilePath))
                {
                    stats.SkipNavigation("member of a module with no file (.NET namespace or stdlib)");
                    continue;
                }
                if (access.MemberNameLineStart <= 0 || access.Member.Length == 0)
                {
                    stats.SkipNavigation("member access with no member token (parser recovery)");
                    continue;
                }
                if (!module.Exports.TryGetValue(access.Member, out var export))
                {
                    stats.SkipNavigation("member is not an export of the module (the program refuses it)");
                    continue;
                }
                var expected = new SCG.List<string> { module.FilePath };
                if (export is Sharpy.Compiler.Semantic.ModuleSymbol { FilePath.Length: > 0 } submodule)
                    expected.Add(submodule.FilePath);
                else if (!string.IsNullOrEmpty(export.DeclaringFilePath))
                    expected.Add(export.DeclaringFilePath);
                cells.Add(new NavCell("member", $"{Spell(access.Object)}.{access.Member}",
                    access.MemberNameLineStart, access.MemberNameColumnStart, expected));
            }

            foreach (var cell in cells)
            {
                stats.CountNavigationCell(cell.Kind);
                var response = await handler.Handle(
                    new LspModels.DefinitionParams
                    {
                        TextDocument = new LspModels.TextDocumentIdentifier(uri),
                        Position = new LspModels.Position(cell.Line - 1, cell.Column - 1),
                    },
                    ct).ConfigureAwait(false);
                var location = response?.Select(l => l.Location).FirstOrDefault(l => l != null);
                var actual = location?.Uri.GetFileSystemPath();
                var match = actual != null && cell.ExpectedFiles.Any(e =>
                    PathNormalizer.Normalize(e) == PathNormalizer.Normalize(actual));

                var expectedText = $"{cell.Kind} {cell.Spelling} -> " +
                    string.Join("|", cell.ExpectedFiles.Select(e => RelativeToRoot(tempRoot, e)).Distinct());
                var actualText = actual == null ? "none" : RelativeToRoot(tempRoot, actual);
                comparisons.Add(new Comparison(EntryLspProjectNav,
                    $"{fixture.TestName}::{fileRel}:{cell.Line}:{cell.Column}",
                    new[] { expectedText }, new[] { actualText },
                    match ? Array.Empty<string>() : new[] { actualText },
                    match ? Array.Empty<string>() : new[] { expectedText },
                    match));
            }
        }
    }

    /// <summary>
    /// The module a member-access receiver binds: an identifier bound to a module, or a nested
    /// <c>a.b</c> whose outer module exports <c>b</c> as a module; the receiver's module type
    /// answers when the binding has no recorded symbol.
    /// </summary>
    private static Sharpy.Compiler.Semantic.ModuleSymbol? ResolveModuleReceiver(Expression receiver, ISemanticQuery query)
        => receiver switch
        {
            Identifier id => query.GetIdentifierSymbol(id) as Sharpy.Compiler.Semantic.ModuleSymbol
                ?? (query.GetEffectiveType(id) as Sharpy.Compiler.Semantic.ModuleType)?.Symbol,
            MemberAccess nested =>
                (ResolveModuleReceiver(nested.Object, query) is { } outer
                    && outer.Exports.TryGetValue(nested.Member, out var inner)
                    ? inner as Sharpy.Compiler.Semantic.ModuleSymbol
                    : null)
                ?? (query.GetEffectiveType(nested) as Sharpy.Compiler.Semantic.ModuleType)?.Symbol,
            _ => null,
        };

    /// <summary>
    /// The files a definition on <c>from module import name</c> may land in, read from the project's
    /// own ASTs: <c>module.name</c>'s file when <c>name</c> is a submodule, else <c>module</c>'s file —
    /// plus, when that module itself re-exports <c>name</c> (<c>from x import name</c>, or a
    /// <c>from x import *</c> that may carry it, as a package <c>__init__</c> does), the files
    /// that re-export resolves to. No symbol the handler reads takes part.
    /// </summary>
    private static void CollectFromImportTargets(
        string moduleName, string name, SCG.IReadOnlyDictionary<string, string> unitsByModule,
        SCG.IReadOnlyDictionary<string, Module?> unitsByFile, SCG.List<string> targets, int depth)
    {
        if (depth > 8)
            return;
        if (unitsByModule.TryGetValue($"{moduleName}.{name}", out var submodule))
        {
            if (!targets.Contains(submodule))
                targets.Add(submodule);
            return;
        }
        if (!unitsByModule.TryGetValue(moduleName, out var file))
            return;
        if (!targets.Contains(file))
            targets.Add(file);
        if (!unitsByFile.TryGetValue(file, out var ast) || ast == null)
            return;
        foreach (var reExport in ast.Body.OfType<FromImportStatement>())
        {
            if (reExport.Module.StartsWith('.'))
                continue;
            var source = reExport.ModuleParts.IsDefaultOrEmpty
                ? reExport.Module
                : string.Join(".", reExport.ModuleParts);
            if (reExport.ImportAll)
            {
                CollectFromImportTargets(source, name, unitsByModule, unitsByFile, targets, depth + 1);
                continue;
            }
            foreach (var alias in reExport.Names)
            {
                if (string.Equals(alias.AsName ?? alias.Name, name, StringComparison.Ordinal))
                    CollectFromImportTargets(source, alias.Name, unitsByModule, unitsByFile, targets, depth + 1);
            }
        }
    }

    /// <summary>The name a top-level statement defines (function, type, alias), or null.</summary>
    private static string? TopLevelDefinitionName(Statement statement) => statement switch
    {
        DecoratedStatement decorated => TopLevelDefinitionName(decorated.Statement),
        FunctionDef f => f.Name,
        ClassDef c => c.Name,
        StructDef s => s.Name,
        InterfaceDef i => i.Name,
        EnumDef e => e.Name,
        TypeAlias t => t.Name,
        _ => null,
    };

    /// <summary>The dotted spelling of a receiver chain, for the report (<c>a.b</c>, or <c>&lt;expr&gt;</c>).</summary>
    private static string Spell(Expression expression) => expression switch
    {
        Identifier id => id.Name,
        MemberAccess access => $"{Spell(access.Object)}.{access.Member}",
        _ => "<expr>",
    };

    private static IEnumerable<Node> EnumerateNodes(Node root)
    {
        var stack = new SCG.Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            foreach (var child in node.GetChildNodes())
            {
                if (child != null)
                    stack.Push(child);
            }
        }
    }

    private sealed record NavCell(string Kind, string Spelling, int Line, int Column, IReadOnlyList<string> ExpectedFiles);

    /// <summary>
    /// Where an unstamped project-bag diagnostic belongs (plan-6ce1a5 D5): the project's
    /// <c>EntryPoint</c>, else its first source file (a library has no entry point).
    /// </summary>
    private static string UnattributedTargetFile(ProjectConfig config)
        => config.EntryPoint != null
            ? Path.Combine(config.ProjectDirectory, config.EntryPoint)
            : config.SourceFiles[0];

    /// <summary>Copies the fixture tree, skipping build output (<c>bin/</c>, <c>obj/</c>, …).</summary>
    private static void CopyFixtureTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (CrashBundleWriter.IsNonSourceSegment(relative))
                continue;
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    /// <summary>
    /// The <c>.spyproj</c> equivalent of <c>MakeConfig</c>: same root namespace, output type, entry
    /// point and file list, plus the fixture's features.
    /// </summary>
    private static void WriteProjectFile(
        string root, IEnumerable<string> relativeSources, string entryPoint, IReadOnlyList<string> features)
    {
        var items = string.Concat(relativeSources.Select(r =>
            $"        <SpyFile Include=\"{System.Security.SecurityElement.Escape(r)}\" />\n"));
        var featuresElement = features.Count > 0
            ? $"        <Features>{string.Join(";", features)}</Features>\n"
            : "";
        File.WriteAllText(Path.Combine(root, "project.spyproj"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<Project>\n" +
            "    <PropertyGroup>\n" +
            "        <RootNamespace>Test</RootNamespace>\n" +
            "        <OutputType>exe</OutputType>\n" +
            $"        <EntryPoint>{System.Security.SecurityElement.Escape(entryPoint)}</EntryPoint>\n" +
            featuresElement +
            "    </PropertyGroup>\n" +
            "    <ItemGroup>\n" +
            items +
            "    </ItemGroup>\n" +
            "</Project>\n");
    }

    private static string RelativeToRoot(string root, string path)
        => Path.GetRelativePath(root, path).Replace('\\', '/');

    private static ModuleRegistry BuildProjectRegistry(string[] references, ProjectConfig config)
    {
        var registry = new ModuleRegistry(Sharpy.Compiler.Logging.NullLogger.Instance);
        foreach (var reference in references)
            registry.LoadReference(reference);
        foreach (var reference in config.References)
            registry.LoadReference(reference);
        foreach (var modulePath in config.ModulePaths)
            registry.AddModulePath(modulePath);
        return registry;
    }

    // ---- signature / comparison ----

    /// <summary>N1: the in-scope diagnostic-code multiset, sorted for a stable multiset comparison.</summary>
    private static IReadOnlyList<string> Signature(IEnumerable<CompilerDiagnostic> diagnostics)
        => diagnostics.Where(IsInScope).Select(d => d.Code!).OrderBy(c => c, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Name-collision refusals that are NUMBERED in the code-generation band but EMITTED before code
    /// generation, so every entry point can produce them and the band must not hide them.
    /// </summary>
    /// <remarks>
    /// Numbering debt: both codes predate the passes that emit them today. SPY0522 comes from
    /// <c>LocalNameCollisionValidator</c> (Validation) and from <c>CodeGenInfoComputer</c>, which
    /// <c>TypeChecker.CheckModule</c> runs during semantic analysis; SPY0523 comes from that same
    /// computer. Leaving them behind the [1, 499] band made every collision fixture vacuous here —
    /// #1268's delegate repro emits SPY0522 and nothing else, so all four entry points were compared
    /// on an empty multiset and would have agreed no matter how far they had drifted.
    /// <para>
    /// Deliberately NOT admitted: SPY0520, which only the emitter ever produced (retired by #2039 —
    /// every module is a namespace, so a type named like its file is no refusal), and SPY0521, which
    /// is reserved and never emitted. This is an exception for two named codes, not a widening of
    /// the band to 5xx.
    /// </para>
    /// </remarks>
    private static readonly SCG.HashSet<string> FrontEndEmittedCollisionCodes = new(StringComparer.Ordinal)
    {
        DiagnosticCodes.CodeGen.MemberNameCollision,
        DiagnosticCodes.CodeGen.FunctionModuleClassCollision,
    };

    private static bool IsInScope(CompilerDiagnostic d)
    {
        if (d.Severity is not (CompilerDiagnosticSeverity.Error or CompilerDiagnosticSeverity.Warning))
            return false;
        var match = SpyCode.Match(d.Code ?? "");
        if (!match.Success)
            return false;
        if (FrontEndEmittedCollisionCodes.Contains(d.Code!))
            return true;
        var n = int.Parse(match.Groups[1].Value);
        return n is >= 1 and <= 499;
    }

    private static Comparison Compare(string entryPoint, string fixture,
        IReadOnlyList<string> baselineCodes, IReadOnlyList<string> entryCodes)
    {
        var (added, removed) = MultisetDiff(baselineCodes, entryCodes);
        return new Comparison(entryPoint, fixture, baselineCodes, entryCodes, added, removed,
            Match: added.Count == 0 && removed.Count == 0);
    }

    /// <summary>Multiset diff: <paramref name="added"/> are codes the entry emits beyond the baseline,
    /// <paramref name="removed"/> are baseline codes the entry drops.</summary>
    private static (SCG.List<string> Added, SCG.List<string> Removed) MultisetDiff(
        IReadOnlyList<string> baseline, IReadOnlyList<string> other)
    {
        var b = baseline.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var o = other.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var added = new SCG.List<string>();
        var removed = new SCG.List<string>();
        foreach (var key in b.Keys.Union(o.Keys))
        {
            var delta = o.GetValueOrDefault(key) - b.GetValueOrDefault(key);
            for (var i = 0; i < delta; i++)
                added.Add(key);
            for (var i = 0; i < -delta; i++)
                removed.Add(key);
        }
        added.Sort(StringComparer.Ordinal);
        removed.Sort(StringComparer.Ordinal);
        return (added, removed);
    }

    /// <summary>True when the module has a top-level function named <c>main</c> (unwrapping a
    /// decorator wrapper), which the REPL's synthesized <c>main()</c> would collide with (N4).</summary>
    private static bool DefinesTopLevelMain(Module? module)
    {
        if (module == null)
            return false;
        foreach (var statement in module.Body)
        {
            var inner = statement is DecoratedStatement decorated ? decorated.Statement : statement;
            if (inner is FunctionDef { Name: "main" })
                return true;
        }
        return false;
    }

    // ---- sampling ----

    private static bool IsAlwaysIncluded(TestFixtureInfo fixture)
    {
        if (fixture.Features.Count > 0)
            return true;
        var name = fixture.TestName;
        return name.Contains("repl", StringComparison.OrdinalIgnoreCase)
            || name.Contains("emit", StringComparison.OrdinalIgnoreCase)
            || name.Contains("feature", StringComparison.OrdinalIgnoreCase)
            || name.Contains("experimental", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Stable (process-independent) bucket in [0, stride) from an FNV-1a hash of the name,
    /// so sub-sampling is deterministic across runs (<see cref="SweepCorpus.StableBucket"/>).</summary>
    private static int StableBucket(string text, int stride) => SweepCorpus.StableBucket(text, stride);

    // ---- allowlist + report I/O (mirrors InteropConformanceTests) ----

    private static readonly Lazy<string?> AllowlistPath = new(FindAllowlistPath);

    private static bool AllowlistFileExists() => AllowlistPath.Value != null && File.Exists(AllowlistPath.Value);

    private static Allowlist LoadAllowlist()
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var wildcards = new SCG.List<string>();
        var path = AllowlistPath.Value;
        if (path == null || !File.Exists(path))
            return new Allowlist(exact, wildcards);

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw;
            var hash = line.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
                line = line.Substring(0, hash);
            line = line.Trim();
            if (line.Length == 0)
                continue;
            if (line.Contains('*', StringComparison.Ordinal))
                wildcards.Add(line);
            else
                exact.Add(line);
        }
        return new Allowlist(exact, wildcards);
    }

    private static string? FindAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var candidate = Path.Combine(current, "src", "Sharpy.Lsp.Tests", "Conformance", "frontend-parity-allowlist.txt");
            if (File.Exists(candidate))
                return candidate;
            var dir = Path.Combine(current, "src", "Sharpy.Lsp.Tests", "Conformance");
            if (Directory.Exists(dir))
                return candidate;
            current = Directory.GetParent(current)?.FullName;
        }
        return null;
    }

    private void WriteReport(
        int discovered, int swept, IReadOnlyCollection<Comparison> comparisons,
        IReadOnlyCollection<Comparison> violations, IReadOnlyCollection<CrashRecord> crashes,
        IReadOnlyCollection<SkipRecord> skips, Allowlist allowlist, int nonAllowlistedViolations,
        IReadOnlyList<string> staleAllowlistEntries, int multiFileFixtures, ProjectModeStats projectStats)
    {
        var byEntryPoint = comparisons
            .GroupBy(c => c.EntryPoint)
            .ToDictionary(g => g.Key, g => new
            {
                compared = g.Count(),
                matched = g.Count(c => c.Match),
                violated = g.Count(c => !c.Match),
            });

        var skipsByReason = skips
            .GroupBy(s => s.EntryPoint + " :: " + s.Reason)
            .OrderByDescending(g => g.Count())
            .Select(g => new { reason = g.Key, count = g.Count() });

        // Signature coverage proves the sweep is NOT vacuously green: it records how many comparisons
        // carried a non-empty in-scope diagnostic multiset (so agreement is meaningful, not "both
        // empty"), the widest signature seen, and the distinct in-scope codes that actually flowed
        // through the comparison — plus a sample of matched non-empty comparisons.
        // Navigation cells carry locations, not diagnostic codes, so they stay out of this block.
        var codeComparisons = comparisons.Where(c => c.EntryPoint != EntryLspProjectNav).ToList();
        var withDiagnostics = codeComparisons.Count(c => c.BaselineCodes.Count > 0);
        var distinctCodes = codeComparisons.SelectMany(c => c.BaselineCodes).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();
        var signatureCoverage = new
        {
            comparisonsWithNonEmptySignature = withDiagnostics,
            comparisonsWithEmptySignature = codeComparisons.Count - withDiagnostics,
            widestBaselineSignature = codeComparisons.Count == 0 ? 0 : codeComparisons.Max(c => c.BaselineCodes.Count),
            distinctInScopeCodesObserved = distinctCodes.Count,
            distinctInScopeCodes = distinctCodes,
            sampleMatchedNonEmpty = codeComparisons
                .Where(c => c.Match && c.BaselineCodes.Count > 0)
                .Take(15)
                .Select(c => new { c.EntryPoint, c.Fixture, codes = c.BaselineCodes }),
        };

        var report = new
        {
            summaryStats = new
            {
                fixturesDiscovered = discovered,
                fixturesSwept = swept,
                entryPointsCompared = new[]
                {
                    EntryCompile, EntryLsp, EntryRepl, EntryCompileProject, EntryEmitDiagnostics,
                    EntryLspProject, EntryLspProjectNav,
                },
                comparisons = comparisons.Count,
                matches = comparisons.Count(c => c.Match),
                justifiedNormalizedSkips = skips.Count,
                violations = violations.Count,
                crashes = crashes.Count,
                allowlistSize = allowlist.Count,
                nonAllowlistedViolations,
                staleAllowlistEntries = staleAllowlistEntries.Count,
            },
            ratchetMode = AllowlistFileExists(),
            signatureCoverage,
            // The project-mode pairs state their coverage (#2242): how many multi-file fixtures the
            // LSP-project pair compared, and how many diagnostics its attribution oracle saw land on
            // the file they are stamped with (the vacuity control, asserted > 0). Unstamped
            // project-bag entries are #2127 cells, listed so none is silently routed.
            projectMode = new
            {
                multiFileFixtures,
                lspProjectCompared = comparisons.Count(c => c.EntryPoint == EntryLspProject),
                attributedDiagnostics = projectStats.AttributedDiagnostics,
                attributedNonEntryDiagnostics = projectStats.AttributedNonEntryDiagnostics,
                unstampedProjectBagDiagnostics = projectStats.Unstamped
                    .OrderBy(u => u.Fixture, StringComparer.Ordinal)
                    .ThenBy(u => u.Code, StringComparer.Ordinal)
                    .Select(u => new { u.Fixture, u.Code, u.Message }),
                navigation = new
                {
                    compared = comparisons.Count(c => c.EntryPoint == EntryLspProjectNav),
                    fromCells = projectStats.NavigationCellCount("from"),
                    memberCells = projectStats.NavigationCellCount("member"),
                    skipsByReason = projectStats.NavigationSkips
                        .OrderByDescending(kv => kv.Value)
                        .Select(kv => new { reason = kv.Key, count = kv.Value }),
                },
            },
            normalizationRules = new[]
            {
                "N1 (all pairs): compare only Error/Warning diagnostics with SPYnnnn in [0001,0499] (Lexer/Parser/Semantic/Validation), plus SPY0522/SPY0523 — numbered in the codegen band but emitted during semantic analysis/validation; drop the rest of codegen SPY05xx (including emitter-only SPY0520), infra SPY09xx, info SPY1xxx, and uncoded diagnostics.",
                "N2 (compile, emit-diagnostics): invoke the entry point with the baseline's OutputType (library against Analyze, exe against AnalyzeProject); SPY0403 (MissingMainFunction) is OutputType-driven, not pipeline drift.",
                "N3 (REPL): compare only when ClassifyInput yields a module-level submission; wrapped-executable and pre-parse-rejected inputs are record-and-skipped.",
                "N4 (REPL): skip fixtures that define a top-level main() (collide with the REPL's synthesized main()).",
            },
            deletedRules = new[]
            {
                "N5 (LSP): skipped .features fixtures while SharpyWorkspace could not source workspace features. Deleted with #1149 — the LSP now compares .features fixtures like every other entry point.",
            },
            byEntryPoint,
            skipsByReason,
            violationDetails = violations.OrderBy(v => v.Key, StringComparer.Ordinal).Take(5000).Select(v => new
            {
                key = v.Key,
                entryPoint = v.EntryPoint,
                fixture = v.Fixture,
                allowlisted = allowlist.Matches(v.Key),
                baselineCodes = v.BaselineCodes,
                entryCodes = v.EntryCodes,
                added = v.Added,
                removed = v.Removed,
            }),
            staleAllowlistEntries,
            crashes = crashes.Take(100).Select(c => new { c.Fixture, c.EntryPoint, c.Exception }),
            skips = skips.Take(100).Select(s => new { s.Fixture, s.EntryPoint, s.Reason }),
        };

        var reportDir = ReportDir();
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "frontend-parity-report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine($"Report written to: {reportPath}");
    }

    private static string ReportDir()
        => Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(FrontEndParityTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", ".claude", "tmp"));

    /// <summary>
    /// The language corpus. Formerly a fourth hand-rolled copy of the anchoring walk; the named
    /// root does the same walk once (#1338).
    /// </summary>
    private static string ResolveFixturesPath() => FixtureRoots.CompilerTests.Path;

    private static int ReadIntEnv(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) ? v : fallback;

    // ---- records ----

    private sealed record Comparison(
        string EntryPoint, string Fixture,
        IReadOnlyList<string> BaselineCodes, IReadOnlyList<string> EntryCodes,
        IReadOnlyList<string> Added, IReadOnlyList<string> Removed, bool Match)
    {
        public string Key => $"{EntryPoint}::{Fixture}";
    }

    private sealed record CrashRecord(string Fixture, string EntryPoint, string Exception);

    private sealed record SkipRecord(string Fixture, string EntryPoint, string Reason);

    /// <summary>A project-bag diagnostic with no <c>FilePath</c> (a #2127 cell, plan-6ce1a5 D5).</summary>
    private sealed record UnstampedRecord(string Fixture, string Code, string Message);

    /// <summary>Coverage counters for the project-mode pairs, shared across the parallel sweep.</summary>
    private sealed class ProjectModeStats
    {
        private int _attributed;
        private int _attributedNonEntry;

        /// <summary>Diagnostics found in the result of exactly the file they are stamped with.</summary>
        public int AttributedDiagnostics => Volatile.Read(ref _attributed);

        /// <summary>The subset of <see cref="AttributedDiagnostics"/> on a file other than the entry point.</summary>
        public int AttributedNonEntryDiagnostics => Volatile.Read(ref _attributedNonEntry);

        public ConcurrentBag<UnstampedRecord> Unstamped { get; } = new();

        public void CountAttributed(bool nonEntry)
        {
            Interlocked.Increment(ref _attributed);
            if (nonEntry)
                Interlocked.Increment(ref _attributedNonEntry);
        }

        /// <summary>Navigation cells asked, by kind (<c>from</c> / <c>member</c>).</summary>
        public ConcurrentDictionary<string, int> NavigationCells { get; } = new(StringComparer.Ordinal);

        /// <summary>Navigation candidates record-and-skipped, by reason.</summary>
        public ConcurrentDictionary<string, int> NavigationSkips { get; } = new(StringComparer.Ordinal);

        public int NavigationCellCount(string kind) => NavigationCells.GetValueOrDefault(kind);

        public void CountNavigationCell(string kind) => NavigationCells.AddOrUpdate(kind, 1, (_, n) => n + 1);

        public void SkipNavigation(string reason) => NavigationSkips.AddOrUpdate(reason, 1, (_, n) => n + 1);
    }

    /// <summary>The reviewed parity allowlist: exact <c>pair::fixture</c> keys plus simple
    /// <c>*</c>-glob patterns, each with a justification comment in the file.</summary>
    private sealed class Allowlist
    {
        private readonly HashSet<string> _exact;
        private readonly SCG.List<string> _wildcards;

        public Allowlist(HashSet<string> exact, SCG.List<string> wildcards)
        {
            _exact = exact;
            _wildcards = wildcards;
        }

        public int Count => _exact.Count + _wildcards.Count;

        public IReadOnlyCollection<string> ExactKeys => _exact;

        public bool Matches(string key)
            => _exact.Contains(key) || _wildcards.Any(w => GlobMatch(key, w));

        private static bool GlobMatch(string text, string pattern)
        {
            var parts = pattern.Split('*');
            var pos = 0;
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Length == 0)
                    continue;
                if (i == 0)
                {
                    if (!text.StartsWith(part, StringComparison.Ordinal))
                        return false;
                    pos = part.Length;
                }
                else
                {
                    var idx = text.IndexOf(part, pos, StringComparison.Ordinal);
                    if (idx < 0)
                        return false;
                    pos = idx + part.Length;
                }
            }
            return pattern.EndsWith("*", StringComparison.Ordinal) || pos == text.Length;
        }
    }
}
