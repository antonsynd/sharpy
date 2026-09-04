using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Guards the strict-Optional contract (#1720): emitted C# must never rely on the
/// <c>implicit operator Optional&lt;T&gt;(T value)</c> that <c>Sharpy.Core/Optional.cs</c>
/// retains for C# interop consumers. If the emitter produces <c>Optional&lt;int&gt; x = 42;</c>
/// instead of <c>Optional&lt;int&gt;.Some(42)</c>, this test catches it via Roslyn's
/// semantic-model conversion analysis over the full <c>.expected.cs</c> snapshot corpus.
/// </summary>
[Trait("Category", "Conformance")]
[Collection("HeavyCompilation")]
public class EmittedOptionalConstructionConformanceTests
{
    private readonly ITestOutputHelper _output;

    private static readonly string FixturesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Sharpy.Compiler.Tests", "Integration", "TestFixtures"));

    public EmittedOptionalConstructionConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // Allowlist: known violations that drain on fix (each cites its tracking issue).
    // A non-allowlisted violation fails the test; a stale allowlist entry (file gone
    // or violation fixed) also fails — entries drain, never accumulate.
    //
    // EMPTY: #1747 is fully drained. The null-conditional lowering used to leave its true branch a
    // bare T whenever the member's or method's OWN type was not already Optional; both emitter arms
    // discriminated on the recorded type of the `?.` node, which the checker wraps in BOTH cases.
    // The checker now records the layer it adds (SetNullConditionalOptionalWrap, member/property at
    // TypeChecker.Expressions.Access.cs and the six isNullConditionalCall blocks in Calls.cs) and
    // one emitter seam constructs Optional<T>.Some(...) from it.
    private static readonly HashSet<string> Allowlist = new(StringComparer.Ordinal);

    // Violations seen only through the executing-fixture corpus arm, keyed by fixture name. The
    // snapshot corpus is 142 selected .expected.cs files, so every fixture below executes but is
    // invisible to the snapshot arm. Each entry cites its issue and drains on fix.
    //
    // All of #1755 Class A drained with #1747. What remains is two lowerings that have nothing to
    // do with `?.`.
    private static readonly HashSet<string> FixtureAllowlist = new(StringComparer.Ordinal)
    {
        // #1755 Class C — ??= on an Optional stores a bare RHS (x ??= 42); needs an R-G ruling
        // before it is wrapped rather than refused
        "type_system/null_coalescing_assignment_optional",
    };

    [Fact]
    public void SnapshotCorpus_NoImplicitConversionToOptional()
    {
        var snapshotFiles = Directory.GetFiles(FixturesRoot, "*.expected.cs", SearchOption.AllDirectories);
        Assert.True(snapshotFiles.Length > 0,
            $"No .expected.cs files found under {FixturesRoot} — corpus is empty, guard is vacuous");

        var references = IntegrationTestBase.GetSharedReferences();
        var violations = new List<string>();
        var allowlistedViolations = new List<string>();
        var unresolvedBindings = new List<string>();
        int scannedFiles = 0;
        int scannedExpressions = 0;
        var filesWithViolations = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in snapshotFiles.OrderBy(f => f, StringComparer.Ordinal))
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(
                source,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                path: file);

            var compilation = CSharpCompilation.Create(
                "OptionalConformanceScan",
                new[] { tree },
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                    .WithNullableContextOptions(NullableContextOptions.Enable));

            var model = compilation.GetSemanticModel(tree);
            scannedFiles++;

            var relativePath = Path.GetRelativePath(FixturesRoot, file)
                .Replace(Path.DirectorySeparatorChar, '/');

            // Instrument check (the standing rule for Roslyn source-compilation scans): an
            // expression that binds to an ERROR type has no conversion at all, so GetConversion
            // reports nothing and this scan under-counts SILENTLY. Assert that every snapshot binds
            // with its names resolved before believing any per-file result.
            var unresolved = UnresolvedTypeDiagnostics(compilation);
            if (unresolved.Count > 0)
            {
                unresolvedBindings.Add(
                    $"{relativePath}: {unresolved.Count} unresolved-name diagnostic(s) — " +
                    string.Join("; ", unresolved.Take(5)));
            }

            foreach (var expr in tree.GetRoot().DescendantNodes().OfType<ExpressionSyntax>())
            {
                scannedExpressions++;
                if (IsImplicitOptionalConversion(model, expr))
                {
                    var span = expr.GetLocation().GetLineSpan();
                    var text = expr.ToString().Replace("\n", " ");
                    if (text.Length > 80)
                        text = text[..80] + "...";
                    var entry =
                        $"{relativePath}:{span.StartLinePosition.Line + 1} " +
                        $"implicit T→Optional<T> on: {text}";

                    if (Allowlist.Contains(relativePath))
                    {
                        allowlistedViolations.Add(entry);
                        filesWithViolations.Add(relativePath);
                    }
                    else
                    {
                        violations.Add(entry);
                    }
                }
            }
        }

        _output.WriteLine(
            $"Scanned {scannedFiles} snapshot file(s), {scannedExpressions} expression node(s); "
            + $"{unresolvedBindings.Count} file(s) with unresolved names.");

        Assert.True(unresolvedBindings.Count == 0,
            $"{unresolvedBindings.Count} snapshot(s) did not bind with every name resolved, so the "
            + "conversion scan under-counts on them silently (an expression bound to an error type "
            + "has no conversion to report). Fix the scan's compilation, not this assertion:\n" +
            string.Join("\n", unresolvedBindings.Take(25)));
        if (allowlistedViolations.Count > 0)
            _output.WriteLine(
                $"Allowlisted violations ({allowlistedViolations.Count}): " +
                string.Join("; ", allowlistedViolations.Select(v => v.Split(' ')[0])));

        // Stale allowlist entries fail — entries drain on fix, never accumulate.
        var staleEntries = Allowlist.Except(filesWithViolations).ToList();
        Assert.True(staleEntries.Count == 0,
            $"Allowlist has {staleEntries.Count} stale entry/entries (violation fixed — remove them): " +
            string.Join(", ", staleEntries));

        Assert.True(violations.Count == 0,
            $"Emitted C# relies on Optional<T>'s implicit operator in {violations.Count} place(s) " +
            $"— use Optional<T>.Some(v) or Optional<T>.None instead (#1720):\n" +
            string.Join("\n", violations.Take(25)));
    }

    [Fact]
    public void PositiveControl_ImplicitConversionIsDetected()
    {
        var references = IntegrationTestBase.GetSharedReferences();

        const string controlSource = @"
using Sharpy;

public static class PositiveControl
{
    public static void Main()
    {
        Optional<int> x = 1;
    }
}
";
        var tree = CSharpSyntaxTree.ParseText(
            controlSource,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path: "PositiveControl.cs");

        var compilation = CSharpCompilation.Create(
            "OptionalConformanceControl",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var model = compilation.GetSemanticModel(tree);

        bool found = false;
        foreach (var expr in tree.GetRoot().DescendantNodes().OfType<ExpressionSyntax>())
        {
            if (IsImplicitOptionalConversion(model, expr))
            {
                found = true;
                _output.WriteLine($"Positive control detected implicit conversion on: {expr}");
                break;
            }
        }

        Assert.True(found,
            "Positive control failed: `Optional<int> x = 1;` was NOT detected as an implicit " +
            "user-defined conversion to Optional<T>. The scan infrastructure is broken — it would " +
            "pass vacuously on the corpus.");
    }

    /// <summary>
    /// The executing-fixture-corpus arm plan-14853b Decision 8 requires: the snapshot corpus is 142
    /// selected <c>.expected.cs</c> files, so a lowering that only ever appears in an unsnapshotted
    /// fixture would slip past <see cref="SnapshotCorpus_NoImplicitConversionToOptional"/> entirely.
    /// This arm compiles EVERY discovered fixture through the production pipeline, captures the
    /// emitter's own <see cref="CompilationUnitSyntax"/> before anything reparses it (the
    /// <see cref="ReparseEquivalenceConformanceTests"/> corpus enumeration and capture seam), and
    /// applies the SAME <see cref="IsImplicitOptionalConversion"/> scan against the SAME
    /// <see cref="IntegrationTestBase.GetSharedReferences"/> reference set.
    ///
    /// <para>Fixtures whose Sharpy front end reported an error contribute nothing: error fixtures
    /// never reach code generation, so holding their partial output to the contract would assert
    /// noise. A unit the C# stage then refused (SPY0908) IS scanned — an emitted unit that fails to
    /// compile can still carry the implicit conversion this guard is about.</para>
    ///
    /// <para>Categorised <c>GapDiscovery</c> because it compiles the whole fixture corpus: it is a
    /// standing sweep, not an edit-loop test. CI runs the sweeps as separate steps.</para>
    /// </summary>
    [Fact]
    [Trait("Category", "GapDiscovery")]
    public void ExecutingFixtureCorpus_NoImplicitConversionToOptional()
    {
        var fixtures = FixtureDiscoveryHelper.DiscoverFixturesFrom(FixtureRoots.CompilerTests)
            .OrderBy(f => f.TestName, StringComparer.Ordinal)
            .ToList();

        Assert.True(fixtures.Count > 0,
            "No fixtures discovered — corpus is empty, guard is vacuous");

        var references = IntegrationTestBase.GetSharedReferences();
        var violations = new List<string>();
        var allowlistedViolations = new List<string>();
        var unresolvedBindings = new List<string>();
        var fixturesWithViolations = new HashSet<string>(StringComparer.Ordinal);
        int scannedFixtures = 0;
        int scannedUnits = 0;
        int skippedNoUnits = 0;

        foreach (var fixture in fixtures)
        {
            bool frontEndClean;
            IReadOnlyList<CompilationUnitSyntax> units;
            try
            {
                (frontEndClean, units) = CompileFixtureCapturing(fixture);
            }
            catch (Exception ex)
            {
                // A fixture whose compile throws is a separate defect surfaced by
                // FileBasedIntegrationTests, not an Optional-construction divergence.
                _output.WriteLine($"skipped {fixture.TestName}: compile threw {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            if (!frontEndClean || units.Count == 0)
            {
                skippedNoUnits++;
                continue;
            }

            scannedFixtures++;
            scannedUnits += units.Count;

            // ALL units of a multi-file fixture go into ONE compilation. Compiled one at a time,
            // a unit that imports a sibling module cannot resolve it (CS0234 on Sharpy.Test.Lib and
            // friends) and every expression in it binds to an error type — which reports no
            // conversion and would make the scan silently vacuous on exactly the cross-module
            // programs it should cover. Measured: compiling one tree at a time, 49 fixtures'
            // first unit reported unresolved names (CS0234 on Sharpy.Test.<Sibling>, CS0103 on
            // functions imported from one); with the whole fixture compiled together, 0.
            var trees = units
                .Select(u => CSharpSyntaxTree.Create(
                    u, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest)))
                .ToList();

            var compilation = CSharpCompilation.Create(
                "OptionalConformanceFixtureScan",
                trees,
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                    .WithNullableContextOptions(NullableContextOptions.Enable));

            // Instrument check: an expression bound to an error type yields no conversion, so a
            // fixture whose names do not resolve would be scanned vacuously.
            var unresolved = UnresolvedTypeDiagnostics(compilation);
            if (unresolved.Count > 0)
            {
                unresolvedBindings.Add(
                    $"{fixture.TestName}: {unresolved.Count} unresolved-name diagnostic(s) — " +
                    string.Join("; ", unresolved.Take(3)));
            }

            foreach (var tree in trees)
            {
                var model = compilation.GetSemanticModel(tree);

                foreach (var expr in tree.GetRoot().DescendantNodes().OfType<ExpressionSyntax>())
                {
                    if (!IsImplicitOptionalConversion(model, expr))
                        continue;

                    var text = expr.ToString().Replace("\n", " ");
                    if (text.Length > 80)
                        text = text[..80] + "...";
                    var entry = $"{fixture.TestName} implicit T→Optional<T> on: {text}";

                    if (FixtureAllowlist.Contains(fixture.TestName))
                    {
                        allowlistedViolations.Add(entry);
                        fixturesWithViolations.Add(fixture.TestName);
                    }
                    else
                    {
                        violations.Add(entry);
                    }
                }
            }
        }

        _output.WriteLine(
            $"Scanned {scannedUnits} emitter unit(s) across {scannedFixtures} fixture(s); " +
            $"{skippedNoUnits} produced no unit (error fixtures / non-clean front end), out of " +
            $"{fixtures.Count} discovered; {unresolvedBindings.Count} unit(s) with unresolved names.");
        if (allowlistedViolations.Count > 0)
            _output.WriteLine($"Allowlisted violations ({allowlistedViolations.Count}).");

        // The corpus must not be empty of scanned units, or every assertion below is vacuous.
        Assert.True(scannedUnits > 0,
            "No emitter unit was scanned — every fixture was skipped, so this guard proves nothing.");

        Assert.True(unresolvedBindings.Count == 0,
            $"{unresolvedBindings.Count} emitter unit(s) did not bind with every name resolved, so "
            + "the conversion scan under-counts on them silently:\n" +
            string.Join("\n", unresolvedBindings.Take(25)));

        // Stale allowlist entries fail — entries drain on fix, never accumulate.
        var staleEntries = FixtureAllowlist.Except(fixturesWithViolations).ToList();
        Assert.True(staleEntries.Count == 0,
            $"FixtureAllowlist has {staleEntries.Count} stale entry/entries (violation fixed — remove them): " +
            string.Join(", ", staleEntries));

        Assert.True(violations.Count == 0,
            $"Emitted C# relies on Optional<T>'s implicit operator in {violations.Count} place(s) " +
            $"across the executing-fixture corpus — use Optional<T>.Some(v) or Optional<T>.None (#1720):\n" +
            string.Join("\n", violations.Take(100)));
    }

    /// <summary>
    /// Compiles one fixture through the production pipeline with a capturing emitter factory, returning
    /// whether the Sharpy FRONT END was clean and the emitter units it built. Mirrors
    /// <c>ReparseEquivalenceConformanceTests.CompileFixture</c>.
    /// </summary>
    private (bool FrontEndClean, IReadOnlyList<CompilationUnitSyntax> Units) CompileFixtureCapturing(
        TestFixtureInfo fixture)
    {
        var factory = new CapturingEmitterFactory();
        var features = fixture.Features.Count == 0
            ? FeatureFlags.None
            : FeatureFlags.None.Enable(fixture.Features);

        if (fixture.IsMultiFile)
        {
            var projectDir = fixture.SpyFilePath;
            var sourceFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.AllDirectories)
                .Where(f => !CrashBundleWriter.IsNonSourceSegment(Path.GetRelativePath(projectDir, f)))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            var config = new ProjectConfig
            {
                ProjectDirectory = projectDir,
                ProjectFilePath = Path.Combine(projectDir, "test.spyproj"),
                RootNamespace = "Sharpy.Test",
                OutputType = "exe",
                EntryPoint = FindFixtureEntryPoint(projectDir),
                SourceFiles = sourceFiles,
                Configuration = "Debug",
                TargetFramework = "net10.0"
            };

            var moduleRegistry = new ModuleRegistry(NullLogger.Instance);
            moduleRegistry.LoadReference(SharpyCoreReference.Location);

            var projectCompiler = new ProjectCompiler(
                NullLogger.Instance, moduleRegistry,
                ProjectCompilerOptions.Default with { Features = features }, factory);
            var result = projectCompiler.Compile(config, CancellationToken.None, emitAssembly: false);
            return (FrontEndClean(result.Diagnostics), factory.Captured);
        }
        else
        {
            var source = File.ReadAllText(fixture.SpyFilePath);
            var options = new CompilerOptions
            {
                References = new[] { SharpyCoreReference.Location },
                OutputType = "exe",
                WarningsAsErrors = false,
                Features = features
            };

            var result = new Compiler(options, NullLogger.Instance, factory)
                .Compile(source, fixture.SpyFilePath);
            return (FrontEndClean(result.Diagnostics), factory.Captured);
        }
    }

    /// <summary>
    /// True when the Sharpy front end reported no error — every error present is a refusal of a
    /// COMPLETE emitter unit (the C# stage's SPY0908, the parse-invariant net's SPY0599, the
    /// precedence net's SPY0524). Such a unit is still scanned: failing to compile does not excuse
    /// relying on the implicit operator.
    /// </summary>
    private static bool FrontEndClean(DiagnosticBag diagnostics) =>
        !diagnostics.GetErrors().Any(d =>
            d.Code is not (DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                or DiagnosticCodes.CodeGen.InternalGeneratedCSharpParseError
                or DiagnosticCodes.CodeGen.EmittedTreePrecedenceInversion));

    private static string FindFixtureEntryPoint(string projectDir)
    {
        var dirName = Path.GetFileName(projectDir);

        if (File.Exists(Path.Combine(projectDir, "main.spy")))
            return "main.spy";

        if (File.Exists(Path.Combine(projectDir, $"{dirName}.spy")))
            return $"{dirName}.spy";

        var spyFiles = Directory.GetFiles(projectDir, "*.spy").OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (spyFiles.Count > 0)
            return Path.GetFileName(spyFiles[0]);

        throw new InvalidOperationException($"No .spy files found in {projectDir}");
    }

    /// <summary>
    /// Records each emitter-built <see cref="CompilationUnitSyntax"/> before the compiler reparses it,
    /// without altering production code generation.
    /// </summary>
    private sealed class CapturingEmitterFactory : ICodeEmitterFactory
    {
        private readonly RoslynEmitterFactory _inner = new();

        public List<CompilationUnitSyntax> Captured { get; } = new();

        public ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default) =>
            new CapturingEmitter(_inner.Create(context, cancellationToken), Captured);

        private sealed class CapturingEmitter : ICodeEmitter
        {
            private readonly ICodeEmitter _inner;
            private readonly List<CompilationUnitSyntax> _sink;

            public CapturingEmitter(ICodeEmitter inner, List<CompilationUnitSyntax> sink)
            {
                _inner = inner;
                _sink = sink;
            }

            public CompilationUnitSyntax GenerateCompilationUnit(Module module)
            {
                var unit = _inner.GenerateCompilationUnit(module);
                _sink.Add(unit);
                return unit;
            }
        }
    }

    /// <summary>
    /// Positive control for the "0 unresolved names" assertions in both corpus arms. Those are
    /// ABSENCE assertions, and an absence assertion passes vacuously when the detector is broken.
    /// This takes a real snapshot, strips its <c>using</c> directives, and asserts the detector
    /// fires — the same mutation as "drop the usings from the scan's compilation", made permanent
    /// and asserted on every run instead of performed once by hand.
    ///
    /// <para>Measured result the arms depend on: all 142 committed snapshots and every emitter unit
    /// in the fixture corpus bind standalone with ZERO unresolved names, because the emitter writes
    /// its own using directives into each compilation unit (<c>using System;</c>,
    /// <c>System.Collections.Generic</c>, <c>System.Linq</c>, <c>System.Threading.Tasks</c>,
    /// <c>global::Sharpy</c>) and <see cref="IntegrationTestBase.GetSharedReferences"/> supplies the
    /// assemblies. No global-usings injection is needed for THIS corpus, and no file is exempted.
    /// This control is what makes that measured zero mean something.</para>
    /// </summary>
    [Fact]
    public void PositiveControl_UnresolvedNamesAreDetected()
    {
        var snapshot = Path.Combine(FixturesRoot, "optionals", "null_conditional_flatten.expected.cs");
        Assert.True(File.Exists(snapshot), $"Control snapshot missing: {snapshot}");

        var source = File.ReadAllText(snapshot);
        var stripped = string.Join("\n", source.Split('\n')
            .Where(line => !line.TrimStart().StartsWith("using ", StringComparison.Ordinal)));

        Assert.NotEqual(source, stripped);

        var references = IntegrationTestBase.GetSharedReferences();
        var tree = CSharpSyntaxTree.ParseText(
            stripped,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path: "UnresolvedControl.cs");

        var compilation = CSharpCompilation.Create(
            "OptionalConformanceUnresolvedControl",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var unresolved = UnresolvedTypeDiagnostics(compilation);
        _output.WriteLine($"Control detected {unresolved.Count} unresolved-name diagnostic(s).");

        Assert.True(unresolved.Count > 0,
            "Positive control failed: a snapshot with its using directives stripped reported NO "
            + "unresolved-name diagnostic. UnresolvedTypeDiagnostics is broken, so the "
            + "\"0 unresolved\" assertions in both corpus arms pass vacuously.");
    }

    /// <summary>
    /// The unresolved-name diagnostics a scan must have none of before its typed results mean
    /// anything: CS0246 (type or namespace not found), CS0103 (name does not exist) and CS0234
    /// (namespace member not found). An expression whose type failed to bind is an ERROR type, and
    /// <c>GetConversion</c> reports no user-defined conversion for one — so a scan that tolerates
    /// them reports "clean" for reasons that have nothing to do with the property it guards. Other
    /// diagnostics (unused variables, unreachable code, and the deliberate refusals in snapshots of
    /// programs the C# stage rejects) do not affect binding and are ignored.
    /// </summary>
    private static IReadOnlyList<string> UnresolvedTypeDiagnostics(CSharpCompilation compilation)
        => compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error
                && d.Id is "CS0246" or "CS0103" or "CS0234")
            .Select(d => $"{d.Id} at line {d.Location.GetLineSpan().StartLinePosition.Line + 1}: "
                + d.GetMessage())
            .ToList();

    private static bool IsImplicitOptionalConversion(SemanticModel model, ExpressionSyntax expr)
    {
        var conversion = model.GetConversion(expr);
        if (!conversion.IsImplicit || !conversion.IsUserDefined)
            return false;

        var method = conversion.MethodSymbol;
        if (method is null)
            return false;

        var containingType = method.ContainingType;
        if (containingType is null)
            return false;

        return containingType.Name == "Optional"
            && containingType.ContainingNamespace?.Name == "Sharpy"
            && containingType.IsGenericType;
    }
}
