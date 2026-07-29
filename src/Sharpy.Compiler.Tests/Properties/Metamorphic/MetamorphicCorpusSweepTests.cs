using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Metamorphic;

/// <summary>
/// Class-A (#1157) — metamorphic transforms at corpus scale.
///
/// <para><b>Defect class.</b> Emit fragility under semantics-preserving syntactic variation: a
/// program the compiler handles correctly stops compiling — or compiles to uncompilable C# — when it
/// is rewritten into an equivalent form. The canonical instance is #1147: wrapping a callee in
/// redundant parentheses made <c>GenerateCall</c> miss every callee-shape arm and emit <c>(Foo)(5)</c>,
/// which C# re-parses as a cast. Nothing about that bug is generics-specific or parenthesis-specific;
/// it is the general shape "the emitter dispatches on surface syntax the front end already
/// normalized away".</para>
///
/// <para><b>Why the transforms existed and still missed it.</b> The 9 transforms were driven by 12
/// hard-coded sample programs, and every fact bailed out with <c>if (!r2.Success) return;</c> — a
/// transform that broke compilation was silently scored as a pass. This sweep repoints the same
/// transforms at the executing-fixture corpus (~1.5k programs) and makes the compile failure itself
/// the violation.</para>
///
/// <para><b>Contract.</b> For every (fixture, transform) cell where the transform applies, the
/// transformed program must compile with <b>no new diagnostic code</b> beyond the transform's entry
/// in the reviewed allowed-delta table (<see cref="MetamorphicTransforms.AllowedNewDiagnostics"/>),
/// must not raise an internal error (SPY09xx), and must not produce C# that fails to bind. Outcomes:
/// <c>ok</c> / <c>notApplicable</c> (the transform left the source byte-identical) / <c>diagRegression</c>
/// / <c>ice</c> / <c>csLeak</c> / <c>crash</c>.</para>
///
/// <para><b>Scope.</b> Single-file fixtures with an <c>.expected</c> file — i.e. valid, executing
/// programs. Error and warning-only fixtures are excluded by design: a transform is only guaranteed
/// semantics-preserving on a valid program, and "does a rewrite preserve a diagnostic" is a
/// different property with a different contract (see docs/design/gap-discovery-contracts.md).</para>
///
/// <para><b>Ratchet.</b> One aggregated JSON report under <c>.claude/tmp/</c> plus the reviewed
/// allowlist <c>Conformance/metamorphic-allowlist.txt</c>: any violating cell whose
/// <c>transform::fixture</c> key is not allowlisted fails the suite. Every allowlist entry cites the
/// issue that will drain it.</para>
/// </summary>
[Trait("Category", "GapDiscovery")]
[Collection("HeavyCompilation")]
public class MetamorphicCorpusSweepTests : IntegrationTestBase
{
    // ---- outcome buckets ----
    internal const string OutcomeOk = "ok";
    internal const string OutcomeNotApplicable = "notApplicable";
    internal const string OutcomeDiagRegression = "diagRegression";
    internal const string OutcomeIce = "ice";
    internal const string OutcomeCsLeak = "csLeak";
    internal const string OutcomeCrash = "crash";

    private static readonly HashSet<string> FailingOutcomes = new(StringComparer.Ordinal)
    {
        OutcomeDiagRegression, OutcomeIce, OutcomeCsLeak, OutcomeCrash,
    };

    public MetamorphicCorpusSweepTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void MetamorphicCorpusSweep_TransformedFixtures_CompileWithoutDiagnosticRegression()
    {
        var stopwatch = Stopwatch.StartNew();

        var corePath = ResolveAssemblyPath("Sharpy.Core.dll");
        Assert.True(File.Exists(corePath), $"Sharpy.Core.dll not found at {corePath}");
        var api = new CompilerApi(NullLogger.Instance, new[] { corePath });
        var csharpBase = BuildCSharpBaseCompilation();

        var (fixtures, droppedByLimit, limit) = SelectFixtures();
        Assert.True(fixtures.Count > 0, "Metamorphic corpus sweep enumerated zero fixtures.");

        var dop = Math.Max(1, ReadIntEnv("METAMORPHIC_SWEEP_DOP", Math.Min(8, Environment.ProcessorCount)));
        var cells = new ConcurrentBag<CellResult>();
        var baselineFailures = new ConcurrentBag<string>();

        Parallel.ForEach(fixtures, new ParallelOptions { MaxDegreeOfParallelism = dop }, fixture =>
        {
            // Multi-file fixtures are covered by transforming their ENTRY file only and compiling the
            // whole project around it; a transform has no defined meaning across module boundaries.
            var entryPath = fixture.IsMultiFile
                ? Path.Combine(fixture.SpyFilePath, FileBasedIntegrationTestsBase.FindEntryPoint(fixture.SpyFilePath))
                : fixture.SpyFilePath;
            var source = File.ReadAllText(entryPath);
            var fileName = Path.GetFileName(entryPath);
            var features = fixture.Features.Count == 0
                ? FeatureFlags.None
                : FeatureFlags.None.Enable(fixture.Features);

            CompileOutcome baseline;
            try
            {
                baseline = Compile(api, csharpBase, fixture, source, fileName, features);
            }
            catch (Exception ex)
            {
                baselineFailures.Add($"{fixture.TestName}: baseline threw {ex.GetType().Name}: {ex.Message}");
                return;
            }

            // A fixture that does not compile cleanly through this seam has no usable baseline to
            // diff against, so its cells are not attempted. This should be empty — every eligible
            // fixture compiles and runs under FileBasedIntegrationTests — so the count is reported
            // rather than silently absorbed.
            if (!baseline.SharpyClean || baseline.CsErrors.Count > 0)
            {
                baselineFailures.Add(
                    $"{fixture.TestName}: {string.Join(" | ", baseline.Errors.Concat(baseline.CsErrors).Take(3))}");
                return;
            }

            foreach (var transform in MetamorphicTransforms.All)
            {
                var key = $"{transform.Name}::{fixture.TestName}";
                string transformed;
                try
                {
                    transformed = transform.Apply(source);
                }
                catch (Exception ex)
                {
                    cells.Add(new CellResult(key, transform.Name, fixture.TestName, OutcomeCrash,
                        new[] { $"transform threw {ex.GetType().Name}: {ex.Message}" }));
                    continue;
                }

                // No-op skip (the perf lever): a transform that found nothing to rewrite returns the
                // source byte-identical, so there is nothing to compile.
                if (string.Equals(transformed, source, StringComparison.Ordinal))
                {
                    cells.Add(new CellResult(key, transform.Name, fixture.TestName, OutcomeNotApplicable,
                        Array.Empty<string>()));
                    continue;
                }

                cells.Add(Evaluate(api, csharpBase, key, transform, fixture, baseline, transformed, fileName, features));
            }
        });

        stopwatch.Stop();

        var results = cells.ToList();
        var allowlist = Allowlist.Load();
        var failures = results.Where(r => FailingOutcomes.Contains(r.Outcome)).ToList();
        var offenders = failures.Where(f => !allowlist.Contains(f.Key)).ToList();

        // Drain-on-fix enforcement: an allowlisted cell that now passes is a line the fixing batch
        // forgot to delete. Surfacing it as a failure is what keeps the list trending to empty
        // instead of accumulating entries nobody revisits. Only meaningful over a full sweep — a
        // subsampled run has not attempted every allowlisted cell.
        var stale = limit > 0
            ? new List<string>()
            : allowlist.ExactKeys
                .Except(failures.Select(f => f.Key), StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

        var byOutcome = results.GroupBy(r => r.Outcome)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var byTransform = results.GroupBy(r => r.Transform)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(r => r.Outcome).ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal),
                StringComparer.Ordinal);

        WriteReport(new
        {
            summaryStats = new
            {
                fixturesEligible = fixtures.Count + droppedByLimit,
                fixturesSwept = fixtures.Count,
                multiFileFixturesSwept = fixtures.Count(f => f.IsMultiFile),
                fixturesDroppedByLimit = droppedByLimit,
                fixtureLimit = limit,
                baselineNotCompiling = baselineFailures.Count,
                transforms = MetamorphicTransforms.All.Count,
                cellsEnumerated = results.Count,
                ok = byOutcome.GetValueOrDefault(OutcomeOk),
                notApplicable = byOutcome.GetValueOrDefault(OutcomeNotApplicable),
                diagRegression = byOutcome.GetValueOrDefault(OutcomeDiagRegression),
                ice = byOutcome.GetValueOrDefault(OutcomeIce),
                csLeak = byOutcome.GetValueOrDefault(OutcomeCsLeak),
                crash = byOutcome.GetValueOrDefault(OutcomeCrash),
                allowlistSize = allowlist.Count,
                failures = failures.Count,
                nonAllowlistedFailures = offenders.Count,
                staleAllowlistEntries = stale.Count,
                wallSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 1),
                degreeOfParallelism = dop,
            },
            ratchetMode = Allowlist.FileExists(),
            scopeNotes = new[]
            {
                "Corpus: every fixture with an .expected sidecar (valid executing programs), .features threaded exactly as FileBasedIntegrationTests threads them. Multi-file fixtures participate through their ENTRY file only — the transform rewrites main.spy and the whole project is compiled around it; a transform has no defined meaning across module boundaries. .error/.warning-only fixtures are excluded by design — transforms are only guaranteed semantics-preserving on valid programs.",
                "Cells compile in library-equivalent mode (no execution, no assembly emit); the output-invariance property lives in MetamorphicCorpusInvarianceTests (Category=RandomProperty).",
                "Comparison is over ERROR+WARNING diagnostic codes. A code present in the transformed compile and absent from the baseline is a regression unless the transform's allowed-delta entry lists it.",
                "Outcomes: ok; notApplicable (transform returned the source byte-identical); diagRegression (new diagnostic code, or the transformed program stopped compiling); ice (SPY09xx internal error); csLeak (Sharpy accepted the program but the generated C# fails to bind while the baseline's bound clean); crash (transform or compiler threw).",
                "baselineNotCompiling counts fixtures whose UNTRANSFORMED source did not compile clean through this seam — no baseline to diff, so their cells are not attempted.",
            },
            byOutcome,
            byTransform,
            baselineNotCompiling = baselineFailures.OrderBy(x => x, StringComparer.Ordinal),
            staleAllowlistEntries = stale,
            violations = failures
                .OrderBy(r => r.Key, StringComparer.Ordinal)
                .Select(r => new
                {
                    key = r.Key,
                    transform = r.Transform,
                    fixture = r.Fixture,
                    outcome = r.Outcome,
                    allowlisted = allowlist.Contains(r.Key),
                    diagnostics = r.Diagnostics,
                    // What the transform actually changed, and the program the compiler was handed,
                    // so triage never has to re-run the transform by hand.
                    changedLines = ChangedLines(r.Fixture, r.TransformedSource),
                    transformedSource = Truncate(r.TransformedSource),
                }),
        });
        WriteFailureKeys(failures.Select(f => f.Key).Distinct().OrderBy(k => k, StringComparer.Ordinal));

        Output.WriteLine($"Fixtures swept: {fixtures.Count} (baseline not compiling: {baselineFailures.Count})");
        Output.WriteLine($"Cells: {results.Count}  ok={byOutcome.GetValueOrDefault(OutcomeOk)} notApplicable={byOutcome.GetValueOrDefault(OutcomeNotApplicable)}");
        Output.WriteLine($"diagRegression={byOutcome.GetValueOrDefault(OutcomeDiagRegression)} ice={byOutcome.GetValueOrDefault(OutcomeIce)} csLeak={byOutcome.GetValueOrDefault(OutcomeCsLeak)} crash={byOutcome.GetValueOrDefault(OutcomeCrash)}");
        Output.WriteLine($"Allowlist size: {allowlist.Count}  Non-allowlisted failures: {offenders.Count}  Stale entries: {stale.Count}");
        Output.WriteLine($"Wall clock: {stopwatch.Elapsed.TotalSeconds:F1}s at DOP {dop}");
        foreach (var offender in offenders.Take(50))
            Output.WriteLine($"  OFFENDER {offender.Key} [{offender.Outcome}] {offender.Diagnostics.FirstOrDefault()}");

        Assert.True(results.Count > 0, "Metamorphic corpus sweep enumerated zero cells.");

        if (Allowlist.FileExists())
        {
            Assert.True(offenders.Count == 0,
                $"Metamorphic corpus sweep: {offenders.Count} cell(s) violate the compile-clean contract and are not " +
                "on the reviewed allowlist. Each is either a real emit/analysis fragility (fix the compiler, or file " +
                "an issue and add an allowlist entry citing it), a missing allowed-delta entry, or a transform that is " +
                "not actually semantics-preserving on that shape (fix the transform).\n" +
                string.Join("\n", offenders.Take(40).Select(o => $"  {o.Key} [{o.Outcome}] {o.Diagnostics.FirstOrDefault()}")) +
                "\nFull report: .claude/tmp/metamorphic-corpus-report.json");

            Assert.True(stale.Count == 0,
                $"Metamorphic corpus sweep: {stale.Count} allowlist entr(ies) no longer fail. Whatever fixed them " +
                "must delete them — an allowlist that outlives its bugs stops being a ratchet. Remove these lines " +
                "from src/Sharpy.Compiler.Tests/Conformance/metamorphic-allowlist.txt:\n" +
                string.Join("\n", stale.Take(40).Select(k => $"  {k}")));
        }
    }

    // ----------------------------------------------------------------------------------------- //
    // Corpus selection
    // ----------------------------------------------------------------------------------------- //

    /// <summary>
    /// Single-file fixtures with an <c>.expected</c> sidecar and no <c>.error</c>/<c>.runtime-error</c>
    /// sidecar — the valid, executing programs the transforms are defined on. Used by the execution
    /// half (<see cref="MetamorphicCorpusInvarianceTests"/>), which runs one program per pair and so
    /// stays single-file; the compile-clean sweep itself covers multi-file fixtures too.
    /// </summary>
    internal static List<TestFixtureInfo> EligibleFixtures()
        => AllEligibleFixtures().Where(fx => !fx.IsMultiFile).ToList();

    /// <summary>
    /// Every eligible fixture, single- and multi-file. Multi-file fixtures participate through their
    /// entry file (see the sweep loop); the same <c>.expected</c>-and-no-<c>.error</c> rule applies.
    /// </summary>
    private static List<TestFixtureInfo> AllEligibleFixtures()
        => FixtureDiscoveryHelper.DiscoverFixtures()
            .Where(fx => fx.ExpectedFile is not null
                      && fx.ErrorFile is null
                      && fx.RuntimeErrorFile is null)
            .OrderBy(fx => fx.TestName, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The swept subset. <c>METAMORPHIC_SWEEP_LIMIT</c> caps it for local experiments; the cap is
    /// deterministic (stable hash over the fixture name, so the sample is a spread of categories
    /// rather than an alphabetical prefix) and the dropped count is reported — never a silent
    /// truncation. Unset/0 means the whole corpus.
    /// </summary>
    private static (List<TestFixtureInfo> Fixtures, int Dropped, int Limit) SelectFixtures()
    {
        var eligible = AllEligibleFixtures();
        var limit = ReadIntEnv("METAMORPHIC_SWEEP_LIMIT", 0);
        if (limit <= 0 || eligible.Count <= limit)
            return (eligible, 0, 0);

        var sampled = eligible
            .OrderBy(fx => StableHash(fx.TestName))
            .ThenBy(fx => fx.TestName, StringComparer.Ordinal)
            .Take(limit)
            .OrderBy(fx => fx.TestName, StringComparer.Ordinal)
            .ToList();
        return (sampled, eligible.Count - sampled.Count, limit);
    }

    // ----------------------------------------------------------------------------------------- //
    // Cell evaluation
    // ----------------------------------------------------------------------------------------- //

    private CellResult Evaluate(
        CompilerApi api, CSharpCompilation csharpBase, string key, IAstTransform transform,
        TestFixtureInfo fixture, CompileOutcome baseline, string transformed, string fileName,
        FeatureFlags features)
    {
        CompileOutcome result;
        try
        {
            result = Compile(api, csharpBase, fixture, transformed, fileName, features);
        }
        catch (Exception ex)
        {
            return new CellResult(key, transform.Name, fixture.TestName, OutcomeCrash,
                new[] { $"{ex.GetType().Name}: {ex.Message}" }, transformed);
        }

        var newCodes = result.Codes.Except(baseline.Codes, StringComparer.Ordinal).ToList();

        if (newCodes.Any(IsInfrastructureCode))
            return new CellResult(key, transform.Name, fixture.TestName, OutcomeIce, result.Errors, transformed);

        var allowed = MetamorphicTransforms.AllowedNewDiagnostics(transform);
        var unexpected = newCodes.Where(c => !allowed.Contains(c)).ToList();
        if (unexpected.Count > 0)
            return new CellResult(key, transform.Name, fixture.TestName, OutcomeDiagRegression,
                Describe(unexpected, result), transformed);

        if (!result.SharpyClean)
            return new CellResult(key, transform.Name, fixture.TestName, OutcomeDiagRegression,
                result.Errors.Count > 0
                    ? result.Errors
                    : new[] { "transformed program failed to compile without reporting a new diagnostic code" },
                transformed);

        if (result.CsErrors.Count > 0)
            return new CellResult(key, transform.Name, fixture.TestName, OutcomeCsLeak, result.CsErrors, transformed);

        return new CellResult(key, transform.Name, fixture.TestName, OutcomeOk, Array.Empty<string>());
    }

    private static IReadOnlyList<string> Describe(IReadOnlyList<string> codes, CompileOutcome result)
    {
        var messages = result.Diagnostics
            .Where(d => codes.Contains(d.Code, StringComparer.Ordinal))
            .Select(d => $"{d.Code}: {d.Message}")
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToList();
        return messages.Count > 0 ? messages : codes.Select(c => $"{c}: (no message captured)").ToList();
    }

    /// <summary>
    /// Compiles the fixture with <paramref name="source"/> standing in for its entry file, then binds
    /// the generated C# through Roslyn. Binding is what makes a silent mis-emit visible: the Sharpy
    /// phases accept the program, so only the C# compiler can object.
    ///
    /// <para>Single-file fixtures go through the production single-file path (a real on-disk entry
    /// compiled as a synthetic project-of-one-file, exactly like
    /// <see cref="IntegrationTestBase.CompileAndExecute"/>). Multi-file fixtures are copied whole to a
    /// temp directory with the entry file replaced, then compiled through
    /// <see cref="CompilerApi.CompileFile"/> so the local-import closure is walked the way a real
    /// project compile walks it.</para>
    /// </summary>
    private static CompileOutcome Compile(
        CompilerApi api, CSharpCompilation csharpBase, TestFixtureInfo fixture, string source,
        string fileName, FeatureFlags features)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy_metamorphic_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        CompileResult result;
        try
        {
            var options = new CompilerOptions { OutputType = "exe", Features = features };
            if (fixture.IsMultiFile)
            {
                CopyDirectory(fixture.SpyFilePath, dir);
                File.WriteAllText(Path.Combine(dir, fileName), source);
                return CompileProject(csharpBase, dir, fileName, features);
            }
            else
            {
                var path = Path.Combine(dir, fileName);
                File.WriteAllText(path, source);
                result = api.Compile(source, options, path);
            }
        }
        finally
        {
            try
            { Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }

        return BuildOutcome(
            csharpBase,
            result.Success,
            result.Diagnostics
                .Where(d => d.Severity is CompilerDiagnosticSeverity.Error or CompilerDiagnosticSeverity.Warning)
                .ToList(),
            CollectGeneratedCSharpSources(result));
    }

    private static CompileOutcome BuildOutcome(
        CSharpCompilation csharpBase, bool success,
        IReadOnlyList<CompilerDiagnostic> diagnostics, IReadOnlyList<string> generatedCSharp)
    {
        var codes = diagnostics
            .Select(d => d.Code)
            .Where(c => !string.IsNullOrEmpty(c))
            .Select(c => c!)
            .ToHashSet(StringComparer.Ordinal);
        var errors = diagnostics
            .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
            .Select(d => $"{d.Code}: {d.Message}")
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToList();

        var csErrors = success ? BindGeneratedCSharp(csharpBase, generatedCSharp) : new List<string>();
        return new CompileOutcome(success, codes, diagnostics, errors, csErrors);
    }

    /// <summary>
    /// Compiles a multi-file fixture through <see cref="ProjectCompiler"/> with the same
    /// <c>RootNamespace</c> the fixture suite uses. Deliberately NOT the entry-file path
    /// (<see cref="CompilerApi.CompileFile"/>): that path mis-emits imported class references for 36
    /// of the 68 multi-file fixtures (#1171), which would cost their transform coverage until it is
    /// fixed. Entry-point agreement is the front-end parity harness's contract (#1144), not this one's.
    /// </summary>
    private static CompileOutcome CompileProject(
        CSharpCompilation csharpBase, string projectDir, string entryFile, FeatureFlags features)
    {
        var sourceFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var config = new ProjectConfig
        {
            ProjectDirectory = projectDir,
            ProjectFilePath = Path.Combine(projectDir, "metamorphic.spyproj"),
            RootNamespace = "Sharpy.Test",
            OutputType = "exe",
            EntryPoint = entryFile,
            SourceFiles = sourceFiles,
            Configuration = "Debug",
            TargetFramework = "net10.0",
        };

        var registry = new ModuleRegistry(NullLogger.Instance);
        registry.LoadReference(ResolveAssemblyPath("Sharpy.Core.dll"));
        var compiler = new ProjectCompiler(
            NullLogger.Instance, registry, ProjectCompilerOptions.Default with { Features = features });
        var result = compiler.Compile(config);

        var diagnostics = result.Diagnostics.GetAll()
            .Where(d => d.Severity is CompilerDiagnosticSeverity.Error or CompilerDiagnosticSeverity.Warning)
            .ToList();
        var generated = result.GeneratedCSharpFiles
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Value)
            .ToList();

        return BuildOutcome(csharpBase, result.Success, diagnostics, generated);
    }

    /// <summary>Copies a fixture's whole directory tree (its sibling modules and package folders).</summary>
    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(targetDir, Path.GetRelativePath(sourceDir, directory)));
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(targetDir, Path.GetRelativePath(sourceDir, file)), overwrite: true);
    }

    private static IReadOnlyList<string> CollectGeneratedCSharpSources(CompileResult result)
    {
        if (result.GeneratedCSharpFiles.Count > 0)
            return result.GeneratedCSharpFiles
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Value)
                .ToList();
        return result.GeneratedCSharp is { Length: > 0 } single
            ? new[] { single }
            : Array.Empty<string>();
    }

    private static List<string> BindGeneratedCSharp(CSharpCompilation baseCompilation, IReadOnlyList<string> sources)
    {
        var trees = sources
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => CSharpSyntaxTree.ParseText(s))
            .ToArray();
        if (trees.Length == 0)
            return new List<string>();
        return baseCompilation.AddSyntaxTrees(trees)
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"{d.Id}: {d.GetMessage()}")
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToList();
    }

    private static CSharpCompilation BuildCSharpBaseCompilation()
    {
        var refs = new List<MetadataReference>(GetSharedReferences());
        var seen = refs.OfType<PortableExecutableReference>()
            .Select(r => Path.GetFileName(r.FilePath))
            .Where(n => n != null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var binDir = Path.GetDirectoryName(typeof(MetamorphicCorpusSweepTests).Assembly.Location)!;
        foreach (var dll in Directory.GetFiles(binDir, "*.dll"))
        {
            if (!seen.Add(Path.GetFileName(dll)))
                continue;
            try
            { refs.Add(MetadataReference.CreateFromFile(dll)); }
            catch { /* not a managed assembly */ }
        }

        return CSharpCompilation.Create(
            "MetamorphicCorpusSweepBase",
            Array.Empty<SyntaxTree>(),
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    // ----------------------------------------------------------------------------------------- //
    // Helpers, allowlist, report I/O
    // ----------------------------------------------------------------------------------------- //

    private static bool IsInfrastructureCode(string? code)
        => code != null && code.StartsWith("SPY09", StringComparison.Ordinal);

    private static string ResolveAssemblyPath(string dllName)
        => Path.Combine(Path.GetDirectoryName(typeof(MetamorphicCorpusSweepTests).Assembly.Location)!, dllName);

    private static int ReadIntEnv(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v > 0 ? v : fallback;

    /// <summary>Deterministic (process-independent) 32-bit FNV-1a hash for stable subsampling.</summary>
    internal static uint StableHash(string s)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in s)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }
    }

    private const int SourceExcerptLimit = 8000;
    private const int ChangedLineLimit = 20;

    /// <summary>
    /// The lines the transform introduced (present in the transformed program, absent from the
    /// fixture) — the fastest possible read of "what was done to this program".
    /// </summary>
    private static IReadOnlyList<string>? ChangedLines(string fixtureName, string? transformed)
    {
        if (transformed == null || !FixturePaths.Value.TryGetValue(fixtureName, out var path))
            return null;
        var original = File.ReadAllText(path).Split('\n').ToHashSet(StringComparer.Ordinal);
        var lines = transformed.Split('\n');
        var changed = new List<string>();
        for (int i = 0; i < lines.Length && changed.Count < ChangedLineLimit; i++)
        {
            if (!original.Contains(lines[i]))
                changed.Add($"{i + 1}: {lines[i]}");
        }
        return changed;
    }

    /// <summary>Fixture name → the file the transform rewrites (the entry file, for multi-file).</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, string>> FixturePaths = new(
        () => AllEligibleFixtures().ToDictionary(
            fx => fx.TestName,
            fx => fx.IsMultiFile
                ? Path.Combine(fx.SpyFilePath, FileBasedIntegrationTestsBase.FindEntryPoint(fx.SpyFilePath))
                : fx.SpyFilePath,
            StringComparer.Ordinal));

    private static string? Truncate(string? source)
    {
        if (source == null)
            return null;
        return source.Length <= SourceExcerptLimit
            ? source
            : source[..SourceExcerptLimit] + "\n… (truncated)";
    }

    private void WriteReport(object report)
    {
        var reportDir = ReportDir();
        Directory.CreateDirectory(reportDir);
        var path = Path.Combine(reportDir, "metamorphic-corpus-report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Output.WriteLine($"Report written to: {path}");
    }

    private static void WriteFailureKeys(IEnumerable<string> keys)
    {
        var reportDir = ReportDir();
        Directory.CreateDirectory(reportDir);
        File.WriteAllLines(Path.Combine(reportDir, "metamorphic-corpus-failures.txt"), keys);
    }

    private static string ReportDir()
        => Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(MetamorphicCorpusSweepTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", ".claude", "tmp"));

    private sealed record CompileOutcome(
        bool SharpyClean,
        IReadOnlySet<string> Codes,
        IReadOnlyList<CompilerDiagnostic> Diagnostics,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> CsErrors);

    private sealed record CellResult(
        string Key, string Transform, string Fixture, string Outcome, IReadOnlyList<string> Diagnostics,
        string? TransformedSource = null);

    /// <summary>
    /// The reviewed conformance allowlist: exact <c>transform::fixture</c> keys, plus <c>*</c> globs
    /// for a defect that spans a whole transform or fixture family. The FILE's presence arms the
    /// ratchet — it is never deleted, only drained to empty.
    /// </summary>
    internal sealed class Allowlist
    {
        private readonly HashSet<string> _exact;
        private readonly List<string> _globs;

        private Allowlist(HashSet<string> exact, List<string> globs)
        {
            _exact = exact;
            _globs = globs;
        }

        public int Count => _exact.Count + _globs.Count;

        /// <summary>The exact (non-glob) keys, for stale-entry detection.</summary>
        public IReadOnlyCollection<string> ExactKeys => _exact;

        public bool Contains(string key)
            => _exact.Contains(key) || _globs.Any(g => GlobMatch(key, g));

        private static readonly Lazy<string?> PathLazy = new(FindPath);

        public static bool FileExists() => PathLazy.Value != null && File.Exists(PathLazy.Value);

        public static Allowlist Load()
        {
            var exact = new HashSet<string>(StringComparer.Ordinal);
            var globs = new List<string>();
            var path = PathLazy.Value;
            if (path == null || !File.Exists(path))
                return new Allowlist(exact, globs);

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
                    globs.Add(line);
                else
                    exact.Add(line);
            }
            return new Allowlist(exact, globs);
        }

        private static string? FindPath()
        {
            var current = AppContext.BaseDirectory;
            while (current != null)
            {
                var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance");
                if (Directory.Exists(dir))
                    return Path.Combine(dir, "metamorphic-allowlist.txt");
                current = Directory.GetParent(current)?.FullName;
            }
            return null;
        }

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
