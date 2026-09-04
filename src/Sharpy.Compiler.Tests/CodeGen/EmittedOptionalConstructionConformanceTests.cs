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
    // #1747 — the null-conditional lowering does not wrap its true branch when the member's or
    // method's OWN type is a bare T. Both emitter arms discriminate on the recorded type of the
    // `?.` node (RoslynEmitter.Expressions.Access.cs:1396 and :1687), which the checker has already
    // wrapped in Optional in BOTH the "member is Optional" and "member is bare T" cases, so the two
    // are indistinguishable there — the fix needs a checker-recorded fact, not an emitter patch.
    // The discriminating pair is null_conditional_flatten.expected.cs:83 (`.Label`, already
    // Optional<string> — correctly unwrapped) versus :95 (`.Value`, a bare int — the violation).
    private static readonly HashSet<string> Allowlist = new(StringComparer.Ordinal)
    {
        // #1747 — null-conditional lowering emits bare T in the true branch
        "optionals/null_conditional_chaining.expected.cs",
        "optionals/null_conditional_flatten.expected.cs",
    };

    // Violations seen only through the executing-fixture corpus arm, keyed by fixture name. The
    // snapshot corpus is 142 selected .expected.cs files, so every fixture below executes but was
    // invisible to the snapshot arm. Each entry cites its issue and drains on fix.
    private static readonly HashSet<string> FixtureAllowlist = new(StringComparer.Ordinal)
    {
        // #1747 — null-conditional lowering emits bare T in the true branch (the two snapshotted
        // fixtures; same sites the Allowlist above covers)
        "optionals/null_conditional_chaining",
        "optionals/null_conditional_flatten",

        // #1755 Class A — the same #1747 null-conditional lowering in unsnapshotted fixtures
        "null_conditional_optional_member_call_1307",
        "optional_narrowed_ops",
        "optional_result/maybe_chained",
        "optional_result/optional_null_conditional_coalesce",
        "type_system/null_conditional_0004",
        "type_system/optional_null_conditional_chain",
        "type_system/optional_null_conditional_method",
        "type_system/optional_null_conditional_value_type",

        // #1755 Class B — a narrowed Optional read stored back into an Optional slot is not
        // re-wrapped (x += 5 on a narrowed int? emits x = x.Unwrap() + 5)
        "type_system/optional_augmented_assign_narrowing",
        "algorithms/linked_list",

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
            $"Scanned {scannedFiles} snapshot file(s), {scannedExpressions} expression node(s).");
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

            foreach (var unit in units)
            {
                var tree = CSharpSyntaxTree.Create(
                    unit, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));

                var compilation = CSharpCompilation.Create(
                    "OptionalConformanceFixtureScan",
                    new[] { tree },
                    references,
                    new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                        .WithNullableContextOptions(NullableContextOptions.Enable));

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
            $"{fixtures.Count} discovered.");
        if (allowlistedViolations.Count > 0)
            _output.WriteLine($"Allowlisted violations ({allowlistedViolations.Count}).");

        // The corpus must not be empty of scanned units, or every assertion below is vacuous.
        Assert.True(scannedUnits > 0,
            "No emitter unit was scanned — every fixture was skipped, so this guard proves nothing.");

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
