using System.Collections.Concurrent;
using System.Text.Json;
// The enclosing `Sharpy` namespace exposes Sharpy.List<T> (Core's Pythonic list), which shadows
// System's List<T> here; SCG-qualify the handful of concrete lists this harness constructs.
using SCG = System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
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
        var (corePath, stdlibPath) = ResolveStdlibAssemblyPaths();
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

        var dop = Math.Max(1, ReadIntEnv("FRONTEND_PARITY_DOP", Math.Min(8, Environment.ProcessorCount)));
        await Parallel.ForEachAsync(
            sampled,
            new ParallelOptions { MaxDegreeOfParallelism = dop },
            async (fixture, ct) =>
            {
                try
                {
                    if (fixture.IsMultiFile)
                        SweepMultiFile(api, references, fixture, comparisons, crashes);
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

        WriteReport(fixtures.Count, sampled.Count, comparisons, violations, crashes.ToList(),
            skips.ToList(), allowlist, offenders.Count, stale);

        _output.WriteLine($"Fixtures discovered: {fixtures.Count}  swept: {sampled.Count}  (stride {stride})");
        _output.WriteLine($"Comparisons: {comparisons.Count}  Matches: {matches}  Violations: {violations.Count}");
        _output.WriteLine($"Justified-normalized skips: {skips.Count}  Crashes: {crashes.Count}");
        _output.WriteLine($"Allowlist size: {allowlist.Count}  Non-allowlisted violations: {offenders.Count}  Stale allowlist entries: {stale.Count}");

        Assert.True(comparisons.Count > 0, "Front-end parity sweep produced zero comparisons.");

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
        // it (SPY0523 module-class collision, SPY0302 self-import). A nameless baseline could not
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

    private void SweepMultiFile(
        CompilerApi api, string[] references, TestFixtureInfo fixture,
        ConcurrentBag<Comparison> comparisons, ConcurrentBag<CrashRecord> crashes)
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
    }

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
    /// Deliberately NOT admitted: SPY0520, which <c>RoslynEmitter.ModuleClass</c> emits and Analyze
    /// therefore structurally cannot produce (admitting it would manufacture divergences, which is
    /// what N1's band exists to prevent), and SPY0521, which is reserved and never emitted. This is
    /// an exception for two named codes, not a widening of the band to 5xx.
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
    /// so sub-sampling is deterministic across runs.</summary>
    private static int StableBucket(string text, int stride)
    {
        if (stride <= 1)
            return 0;
        uint hash = 2166136261;
        foreach (var ch in text)
        {
            hash ^= ch;
            hash *= 16777619;
        }
        return (int)(hash % (uint)stride);
    }

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
        IReadOnlyList<string> staleAllowlistEntries)
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
        var withDiagnostics = comparisons.Count(c => c.BaselineCodes.Count > 0);
        var distinctCodes = comparisons.SelectMany(c => c.BaselineCodes).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();
        var signatureCoverage = new
        {
            comparisonsWithNonEmptySignature = withDiagnostics,
            comparisonsWithEmptySignature = comparisons.Count - withDiagnostics,
            widestBaselineSignature = comparisons.Count == 0 ? 0 : comparisons.Max(c => c.BaselineCodes.Count),
            distinctInScopeCodesObserved = distinctCodes.Count,
            distinctInScopeCodes = distinctCodes,
            sampleMatchedNonEmpty = comparisons
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
            violationDetails = violations.Take(200).Select(v => new
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

    private static (string CorePath, string StdlibPath) ResolveStdlibAssemblyPaths()
    {
        var baseDir = Path.GetDirectoryName(typeof(FrontEndParityTests).Assembly.Location)!;
        return (Path.Combine(baseDir, "Sharpy.Core.dll"), Path.Combine(baseDir, "Sharpy.Stdlib.dll"));
    }

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
