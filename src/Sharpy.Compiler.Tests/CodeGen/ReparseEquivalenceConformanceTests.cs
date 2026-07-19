using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// The permanent guard for the #1095 zero-parse tree handoff: every compilation unit the
/// <see cref="RoslynEmitter"/> builds must <b>bind the same</b> when handed straight to
/// <c>CSharpCompilation.Create</c> as it does when its printed C# is reparsed. This is the invariant
/// the P5.2 flip (dropping the codegen-time <c>ParseText</c>) depends on.
///
/// <para>
/// <b>Why binding-equivalence, not <c>IsEquivalentTo</c>.</b> The plan first framed this as a structural
/// assertion — <c>Create(unit).IsEquivalentTo(ParseText(unit.ToFullString()))</c>. That is empirically
/// unusable, for two independent reasons discovered while building this suite:
/// <list type="number">
/// <item><description><c>SyntaxNode.IsEquivalentTo</c> is trivia-sensitive in this Roslyn build. For a
/// real <c>print(42)</c> emitter unit the token (kind, text) sequences are byte-identical (54 == 54) yet
/// <c>IsEquivalentTo</c> returns <c>false</c>; it returns <c>false</c> for <em>every</em> factory-built
/// tree versus its own reparse.</description></item>
/// <item><description>Even ignoring trivia, the emitter legitimately builds qualified call targets such
/// as <c>global::Sharpy.Builtins.Print(...)</c> as a <see cref="QualifiedNameSyntax"/>, whereas the C#
/// parser produces a <c>SimpleMemberAccessExpression</c> in expression position. Identical text, different
/// node kind — so a structural walk flags essentially every builtin/stdlib call. Roslyn binds a
/// <c>QualifiedName</c> callee exactly like the member-access form, so this difference is benign and
/// pervasive.</description></item>
/// </list>
/// The property that actually matters for the flip is whether the emitter's node graph <em>binds</em> the
/// same as its reparse. So this suite compiles each corpus member twice — once with the emitter nodes
/// wrapped directly (<see cref="CSharpSyntaxTree.Create(CSharpSyntaxNode, CSharpParseOptions, string, System.Text.Encoding, System.Threading.CancellationToken)"/>)
/// and once from the reparsed text — and asserts the direct-handoff compilation introduces <b>no binding
/// diagnostic the reparse avoids</b> (diagnostics are diffed as a <c>(diagnostic id, file)</c> multiset:
/// no id may appear on the direct-handoff side with higher multiplicity than on the reparse side, which is
/// the reference; anything caused only by the missing reference set — not by the tree shape — appears
/// identically in both and cancels). That catches exactly the reparse-hostile classes #1095 tracks: a
/// <c>global::X.Y</c> name packed into one identifier token (<c>CS0246</c>/<c>CS0103</c>), a variadic
/// <c>params</c> array with an empty rank specifier instead of an <c>OmittedArraySizeExpression</c>
/// (<c>CS0225</c>), an out-of-scope discard identifier, a <c>System.ValueTuple.Create</c> receiver whose
/// name doesn't bind directly, an escaped named argument (<c>@default:</c>) that resolves to the wrong
/// overload (<c>CS1739</c>), and similar — while ignoring trivia and benign structural representation
/// differences.
/// </para>
///
/// <para>
/// <b>Errors and warnings are both compared.</b> Restricting to errors would suffice for the classes above,
/// but warnings are included too because they are empirically <em>silent</em> here: across the whole
/// corpus (stdlib + fixtures) not one warning id ever appears with higher multiplicity on the direct-handoff
/// side — warnings arise identically in both trees and cancel in the multiset diff. Including them makes the
/// guard strictly stronger (it would catch, say, a hiding or obsolete warning introduced only by a bad tree
/// shape) at zero false-positive cost. If a future change ever makes warnings noisy, the correct response is
/// to drop this to errors-only and note it here — not to weaken the multiset rule.
/// </para>
///
/// <para>
/// <b>How the emitter node is captured.</b> The seam that hands the emitter's
/// <see cref="CompilationUnitSyntax"/> to <c>ParseText</c> discards it (see
/// <c>ProjectCompiler.CodeGen.cs</c>). Rather than reparse and lose the pre-parse node, this suite injects
/// a <see cref="CapturingEmitterFactory"/> through the compiler's existing <see cref="ICodeEmitterFactory"/>
/// seam. The real <see cref="RoslynEmitter"/> runs exactly as in production; the factory records the node
/// it returns for each file before anyone reparses it.
/// </para>
///
/// <para>
/// <b>Corpora.</b> (a) Every module of the native spy-stdlib project
/// (<c>src/Sharpy.Stdlib/spy/stdlib.spyproj</c>, compiled the same way as
/// <c>NativeStdlibCompilationTests</c> — the corpus that caught the original break; includes
/// <c>struct_module</c>). (b) The complete file-based fixture corpus under
/// <c>Integration/TestFixtures/</c>: every discovered fixture (single-file and multi-file) is compiled and
/// its emitted units checked. A fixture contributes units only when its compile <em>succeeds</em> — error
/// fixtures never reach code generation, so they contribute nothing and no malformed emitter output is ever
/// asserted. No fixture directory is excluded and no sampling is applied: the corpus is asserted in full.
/// At the time of writing, discovery yields 2,107 fixtures (from ~2,240 <c>.spy</c> files; multi-file
/// fixtures count once); 1,575 compile cleanly and contribute 1,675 emitter units, and 532 produce no unit
/// (the ~540 <c>.error</c> fixtures whose compile is expected to fail, plus any compile that reports errors).
/// Both Facts together run in roughly 25s (stdlib ~2s, fixtures ~23s), so the full corpus is checked with no
/// subset needed.
/// </para>
///
/// <para>
/// <b>Relation to the old invariant.</b> This guard proves the emitter tree <em>binds</em> like its reparse;
/// deeper runtime-behavior safety after the flip comes from the fixture <c>.expected</c> corpus, which
/// executes the Create-compiled programs and checks their output. Together they replace the parser-diagnostic
/// invariant (<c>CompilerInvariants.AssertPostCodeGen</c>) that the zero-parse flip makes vacuous — see the
/// plan's Design Decision 6.
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class ReparseEquivalenceConformanceTests
{
    private readonly ITestOutputHelper _output;
    private readonly ICompilerLogger _logger = NullLogger.Instance;

    private static readonly string StdlibSpyDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Sharpy.Stdlib", "spy"));

    private static readonly string SpyprojPath = Path.Combine(StdlibSpyDir, "stdlib.spyproj");

    /// <summary>
    /// Reference set for compiling the spy-stdlib trees: the shared test references (Sharpy.Core +
    /// framework) plus every managed assembly next to Sharpy.Core in the test output directory
    /// (Sharpy.Stdlib and its NuGet closure). A name that fails to resolve for lack of a reference errors
    /// identically in both the direct-handoff and reparse compilations and so cancels in the diff; the set
    /// only needs to be complete enough that the internal <c>global::Sharpy.*</c> names the #1095 breaks hit
    /// actually resolve.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<MetadataReference>> StdlibReferences =
        new(BuildStdlibReferences);

    public ReparseEquivalenceConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------------------------------------------------------------------------------------------
    // Corpus (a): every module of the native spy-stdlib project.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void StdlibModules_EmitterTrees_BindEquivalentlyUnderDirectHandoff()
    {
        var factory = new CapturingEmitterFactory();
        var result = CompileStdlibProject(factory);

        if (!result.Success)
        {
            foreach (var diag in result.Diagnostics.GetErrors())
                _output.WriteLine($"ERROR: {diag}");
        }

        var units = factory.Captured;
        units.Should().NotBeEmpty(
            "the spy-stdlib project must emit at least one module for this suite to guard anything");

        var extra = ExtraBindingErrors(units, StdlibReferences.Value);

        _output.WriteLine(
            $"Checked {units.Count} stdlib module unit(s) for binding-equivalent direct handoff.");

        extra.Should().BeEmpty(BuildFailureMessage(
            "stdlib module tree(s) introduce a binding error under direct handoff that reparsing avoids (#1095)",
            extra));
    }

    // ---------------------------------------------------------------------------------------------
    // Corpus (b): the full file-based fixture corpus.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void FileBasedFixtures_EmitterTrees_BindEquivalentlyUnderDirectHandoff()
    {
        var fixtures = FixtureDiscoveryHelper.DiscoverFixtures()
            .OrderBy(f => f.TestName, StringComparer.Ordinal)
            .ToList();

        fixtures.Should().NotBeEmpty("the fixture corpus must be discoverable for this suite to guard anything");

        var references = IntegrationTestBase.GetSharedReferences();
        var failures = new List<string>();
        int assertedFixtures = 0;
        int assertedUnits = 0;
        int skippedNoUnits = 0;

        foreach (var fixture in fixtures)
        {
            bool success;
            IReadOnlyList<(string? Path, CompilationUnitSyntax Unit)> units;
            try
            {
                (success, units) = CompileFixture(fixture);
            }
            catch (Exception ex)
            {
                // A fixture whose compile throws is out of scope for reparse-equivalence — that is a
                // separate defect surfaced by FileBasedIntegrationTests, not a tree-shape divergence.
                _output.WriteLine($"skipped {fixture.TestName}: compile threw {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            // Assert only units from a clean compile: error fixtures (and any compile that reported
            // errors) never produce final, valid C#, so their partial emitter output must not be held to
            // the binding-equivalence contract.
            if (!success || units.Count == 0)
            {
                skippedNoUnits++;
                continue;
            }

            assertedFixtures++;
            assertedUnits += units.Count;

            foreach (var e in ExtraBindingErrors(units, references))
                failures.Add($"  fixture {fixture.TestName}: {e}");
        }

        _output.WriteLine(
            $"Checked {assertedUnits} unit(s) across {assertedFixtures} successfully-compiling fixture(s); "
            + $"{skippedNoUnits} fixture(s) produced no unit (error fixtures / non-clean compiles), out of "
            + $"{fixtures.Count} discovered.");

        failures.Should().BeEmpty(BuildFailureMessage(
            "fixture tree(s) introduce a binding error under direct handoff that reparsing avoids (#1095)",
            failures));
    }

    // ---------------------------------------------------------------------------------------------
    // Self-tests: the mechanism flags the known reparse-hostile shapes and accepts the sanctioned /
    // benign ones. These pin the guard against silently going toothless.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Mechanism_FlagsGlobalNameInSingleIdentifierToken()
    {
        // The historical bug: a "global::X.Y" name emitted as one IdentifierName token prints correctly
        // but binds as an unknown type under direct handoff (CS0246), whereas reparsing recognizes the
        // global:: alias.
        var extra = ExtraBindingErrors(
            Single(MinimalFieldUnit((TypeSyntax)IdentifierName("global::System.String"))),
            IntegrationTestBase.GetSharedReferences());

        extra.Should().NotBeEmpty("a global:: name packed into a single identifier token fails to bind directly");
        string.Join("\n", extra).Should().Contain("CS0246");
    }

    [Fact]
    public void Mechanism_FlagsParamsArrayWithEmptyRankSpecifier()
    {
        // Bucket 2: a params parameter whose array type has a bare ArrayRankSpecifier() (empty Sizes list)
        // instead of one OmittedArraySizeExpression binds as CS0225 under direct handoff.
        var paramsType = ArrayType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))
            .WithRankSpecifiers(SingletonList(ArrayRankSpecifier()));
        var parameter = Parameter(Identifier("args"))
            .WithModifiers(TokenList(Token(SyntaxKind.ParamsKeyword)))
            .WithType(paramsType);
        var method = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier("M"))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(parameter)))
            .WithBody(Block());
        var cls = ClassDeclaration("C").WithMembers(SingletonList<MemberDeclarationSyntax>(method));
        var unit = CompilationUnit()
            .WithMembers(SingletonList<MemberDeclarationSyntax>(cls))
            .NormalizeWhitespace();

        var extra = ExtraBindingErrors(Single(unit), IntegrationTestBase.GetSharedReferences());

        extra.Should().NotBeEmpty("an empty-Sizes params array binds as CS0225 under direct handoff");
        string.Join("\n", extra).Should().Contain("CS0225");
    }

    [Fact]
    public void Mechanism_AcceptsPlainType()
    {
        ExtraBindingErrors(
            Single(MinimalFieldUnit(PredefinedType(Token(SyntaxKind.IntKeyword)))),
            IntegrationTestBase.GetSharedReferences())
            .Should().BeEmpty();
    }

    [Fact]
    public void Mechanism_AcceptsSanctionedGlobalQualifiedName()
    {
        // The wrapper builds a real AliasQualifiedNameSyntax with the global keyword — it binds directly.
        var goodType = (TypeSyntax)RoslynEmitter.MakeGlobalQualifiedName("System", "String");

        ExtraBindingErrors(Single(MinimalFieldUnit(goodType)), IntegrationTestBase.GetSharedReferences())
            .Should().BeEmpty();
    }

    [Fact]
    public void Mechanism_AcceptsBenignQualifiedNameCallee()
    {
        // Real emitter output: `print(42)` lowers to an invocation of a QualifiedNameSyntax callee
        // (global::Sharpy.Builtins.Print). That is not structurally equal to the reparsed member-access
        // form, but it binds identically — so binding-equivalence must accept it. This locks the guard
        // against regressing to an over-strict structural comparison.
        var units = CompileSourceCapturing("def main() -> None:\n    print(42)\n", "print.spy");

        units.Should().NotBeEmpty("the emitter must produce a unit for a trivial program");
        ExtraBindingErrors(units, IntegrationTestBase.GetSharedReferences()).Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------------------------
    // Seam pin (#1095, P5.2): the codegen seam must hand the emitter's tree to Roslyn via
    // CSharpSyntaxTree.Create. Green-node identity is the one cheap observable separating Create
    // (re-roots the SAME green tree) from a silent revert to ParseText(ToFullString()) (a fresh
    // parse, fresh green nodes) — the two are behaviorally identical otherwise and differ only in
    // the double-parse cost #1095 removed.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void CodeGenSeam_HandsEmitterTreeToRoslynWithoutReparse()
    {
        var (result, units) = CompileProjectFilesCapturing(
            new[] { ("main.spy", "def main() -> None:\n    print(42)\n") },
            entryPoint: "main.spy");

        result.Success.Should().BeTrue("the trivial program must compile");
        var emitted = units.Should().ContainSingle().Subject.Unit;

        var generatedTree = result.ProjectModel!.Units.Values
            .Select(u => u.GeneratedSyntaxTree)
            .Single(t => t is not null)!;

        generatedTree.GetRoot().IsIncrementallyIdenticalTo(emitted).Should().BeTrue(
            "the seam must pass the emitter's tree via CSharpSyntaxTree.Create — a reparse of the "
            + "generated text produces fresh green nodes and silently reintroduces the #1095 double parse");
    }

    // ---------------------------------------------------------------------------------------------
    // Keyword-named-module coverage (#1095, regression fixed in 664f3332f).
    //
    // The flip (P5.2) un-masked a corpus blind spot: a module whose name is a C# keyword (e.g. a
    // `base.spy`) is referenced through an @-escaped identifier at two sites — the `using @base = ...;`
    // alias name and the `@base.Member(...)` module-reference expression. Both were built with
    // SyntaxFactory.Identifier("@base") (ValueText "@base") instead of the parser's "base", so under
    // direct CSharpSyntaxTree.Create handoff the reference failed to bind (CS0103); only one multi-file
    // integration test caught it because no fixture in the discovered corpus has a keyword-named module.
    // (Keyword-named top-level functions and classes need no such escaping under direct handoff: they are
    // mangled to PascalCase — `lock` -> `Lock` — which is never a C# keyword. A module name, used verbatim
    // in the alias and reference, is the only vector, so that is what this covers.)
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("base")]
    [InlineData("lock")]
    [InlineData("params")]
    public void KeywordNamedModule_EmitterTrees_BindEquivalentlyUnderDirectHandoff(string moduleName)
    {
        var (result, units) = CompileProjectFilesCapturing(
            new[]
            {
                ($"{moduleName}.spy", "def get_value() -> int:\n    return 42\n"),
                ("main.spy", $"import {moduleName}\n\ndef main() -> None:\n    print({moduleName}.get_value())\n"),
            },
            entryPoint: "main.spy");

        result.Success.Should().BeTrue($"the project importing keyword-named module '{moduleName}' must compile");
        units.Should().NotBeEmpty();

        // Guard that the scenario is genuinely exercised: the emitter must @-escape the module name at the
        // using-alias and module-reference sites. If a future change ever stopped emitting the escaped form,
        // this assertion — not just the binding check below — fails loudly rather than passing vacuously.
        string.Join("\n", units.Select(u => u.Unit.ToFullString()))
            .Should().Contain($"@{moduleName}",
                $"the keyword-named module '{moduleName}' must be referenced through an @-escaped identifier");

        ExtraBindingErrors(units, IntegrationTestBase.GetSharedReferences()).Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------------------------
    // Binding-equivalence check.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Compiles the corpus member's emitter units two ways — direct handoff
    /// (<c>CSharpSyntaxTree.Create</c>) and reparse (<c>ParseText(ToFullString())</c>) — against the same
    /// references and options, and returns the binding diagnostics (errors and warnings) present under
    /// direct handoff that reparsing does not produce. Diagnostics are diffed as a <c>(id, file)</c>
    /// multiset with the reparse side as the reference: an id is "extra" only when it appears more times
    /// under direct handoff than under reparse, so anything caused by an incomplete reference set (present
    /// identically in both compilations) cancels and only tree-shape divergences survive. Skips the reparse
    /// compilation entirely when the direct-handoff compilation is already clean (the common case).
    /// </summary>
    private static IReadOnlyList<string> ExtraBindingErrors(
        IReadOnlyList<(string? Path, CompilationUnitSyntax Unit)> units,
        IReadOnlyList<MetadataReference> references)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        var createdDiagnostics = CompilationDiagnostics(
            units.Select(u => CSharpSyntaxTree.Create(u.Unit, path: u.Path ?? "member.cs")),
            references, options);

        if (createdDiagnostics.Count == 0)
            return Array.Empty<string>();

        var reparsedDiagnostics = CompilationDiagnostics(
            units.Select(u => CSharpSyntaxTree.ParseText(
                u.Unit.ToFullString(), path: u.Path ?? "member.cs", encoding: System.Text.Encoding.UTF8)),
            references, options);

        var baseline = new Dictionary<(string Id, string File), int>();
        foreach (var d in reparsedDiagnostics)
        {
            var key = (d.Id, d.Location.SourceTree?.FilePath ?? string.Empty);
            baseline[key] = baseline.TryGetValue(key, out var n) ? n + 1 : 1;
        }

        var extra = new List<string>();
        foreach (var d in createdDiagnostics)
        {
            var key = (d.Id, d.Location.SourceTree?.FilePath ?? string.Empty);
            if (baseline.TryGetValue(key, out var n) && n > 0)
            {
                baseline[key] = n - 1;
                continue;
            }

            var file = Label(d.Location.SourceTree?.FilePath);
            var line = d.Location.GetLineSpan().StartLinePosition.Line + 1;
            extra.Add($"{d.Severity.ToString().ToLowerInvariant()} {d.Id} [{file}:{line}] {d.GetMessage()}");
        }

        return extra;
    }

    private static List<Diagnostic> CompilationDiagnostics(
        IEnumerable<SyntaxTree> trees, IReadOnlyList<MetadataReference> references,
        CSharpCompilationOptions options)
    {
        var compilation = CSharpCompilation.Create("reparse-equivalence", trees, references, options);
        return compilation.GetDiagnostics()
            .Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToList();
    }

    private static string BuildFailureMessage(string headline, IReadOnlyList<string> failures)
    {
        const int cap = 25;
        var body = string.Join("\n", failures.Take(cap));
        if (failures.Count > cap)
            body += $"\n  ... and {failures.Count - cap} more";
        return $"{failures.Count} {headline}.\n"
            + "A qualified name or array shape that prints correctly but does not bind under direct tree "
            + "handoff blocks the zero-parse flip — build it as real syntax (e.g. MakeGlobalQualifiedName, "
            + "OmittedArraySizeExpression), not from a string or an empty rank.\n" + body;
    }

    // ---------------------------------------------------------------------------------------------
    // Compilation harnesses.
    // ---------------------------------------------------------------------------------------------

    private ProjectCompilationResult CompileStdlibProject(ICodeEmitterFactory emitterFactory)
    {
        var config = ProjectFileParser.Load(SpyprojPath, "Debug");

        // Inject Sharpy.Core and Sharpy.Stdlib references (same as the CLI and
        // NativeStdlibCompilationTests do).
        var corePath = SharpyCoreReference.Location;
        if (!config.References.Contains(corePath))
            config.References.Add(corePath);

        var coreDir = Path.GetDirectoryName(corePath)!;
        var stdlibPath = Path.Combine(coreDir, "Sharpy.Stdlib.dll");
        if (File.Exists(stdlibPath) && !config.References.Contains(stdlibPath))
            config.References.Add(stdlibPath);

        var options = new CompilerOptions
        {
            References = config.References.ToArray(),
            WarningsAsErrors = false
        };

        var compiler = new Compiler(options, _logger, emitterFactory);
        return compiler.CompileProject(config);
    }

    private (bool Success, IReadOnlyList<(string? Path, CompilationUnitSyntax Unit)> Units) CompileFixture(
        TestFixtureInfo fixture)
    {
        var factory = new CapturingEmitterFactory();
        var features = fixture.Features.Count == 0
            ? FeatureFlags.None
            : FeatureFlags.None.Enable(fixture.Features);

        if (fixture.IsMultiFile)
        {
            var projectDir = fixture.SpyFilePath;
            var entryPoint = FindEntryPoint(projectDir);
            var sourceFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            var config = new ProjectConfig
            {
                ProjectDirectory = projectDir,
                ProjectFilePath = Path.Combine(projectDir, "test.spyproj"),
                RootNamespace = "Sharpy.Test",
                OutputType = "exe",
                EntryPoint = entryPoint,
                SourceFiles = sourceFiles,
                Configuration = "Debug",
                TargetFramework = "net10.0"
            };

            var moduleRegistry = new ModuleRegistry(_logger);
            moduleRegistry.LoadReference(SharpyCoreReference.Location);

            var projectCompiler = new ProjectCompiler(
                _logger, moduleRegistry, emitterFactory: factory, features: features);
            var result = projectCompiler.Compile(config, CancellationToken.None, emitAssembly: false);
            return (result.Success, factory.Captured);
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

            var compiler = new Compiler(options, _logger, factory);
            var result = compiler.Compile(source, fixture.SpyFilePath);
            return (result.Success, factory.Captured);
        }
    }

    private IReadOnlyList<(string? Path, CompilationUnitSyntax Unit)> CompileSourceCapturing(
        string source, string fileName)
    {
        var factory = new CapturingEmitterFactory();
        var options = new CompilerOptions
        {
            References = new[] { SharpyCoreReference.Location },
            OutputType = "exe"
        };
        _ = new Compiler(options, _logger, factory).Compile(source, fileName);
        return factory.Captured;
    }

    /// <summary>
    /// Writes an in-memory multi-file project to a throwaway temp directory and compiles it through the
    /// same <see cref="ProjectCompiler"/> + capturing-factory path the multi-file fixture corpus uses,
    /// returning the emitter units. Used for scenarios (e.g. keyword-named modules) that need a real
    /// cross-module import but are clearer to express inline than as an on-disk fixture.
    /// </summary>
    private (ProjectCompilationResult Result, IReadOnlyList<(string? Path, CompilationUnitSyntax Unit)> Units)
        CompileProjectFilesCapturing(IReadOnlyList<(string RelPath, string Source)> files, string entryPoint)
    {
        var projectDir = Path.Combine(Path.GetTempPath(), "sharpy_reparse_kw_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectDir);
        try
        {
            var sourceFiles = new List<string>();
            foreach (var (relPath, source) in files)
            {
                var fullPath = Path.Combine(projectDir, relPath);
                File.WriteAllText(fullPath, source);
                sourceFiles.Add(fullPath);
            }
            sourceFiles.Sort(StringComparer.Ordinal);

            var factory = new CapturingEmitterFactory();
            var config = new ProjectConfig
            {
                ProjectDirectory = projectDir,
                ProjectFilePath = Path.Combine(projectDir, "test.spyproj"),
                RootNamespace = "Sharpy.Test",
                OutputType = "exe",
                EntryPoint = entryPoint,
                SourceFiles = sourceFiles,
                Configuration = "Debug",
                TargetFramework = "net10.0"
            };

            var moduleRegistry = new ModuleRegistry(_logger);
            moduleRegistry.LoadReference(SharpyCoreReference.Location);

            var projectCompiler = new ProjectCompiler(
                _logger, moduleRegistry, emitterFactory: factory, features: FeatureFlags.None);
            var result = projectCompiler.Compile(config, CancellationToken.None, emitAssembly: false);
            return (result, factory.Captured);
        }
        finally
        {
            try
            { Directory.Delete(projectDir, recursive: true); }
            catch { /* best-effort temp cleanup */ }
        }
    }

    private static IReadOnlyList<MetadataReference> BuildStdlibReferences()
    {
        var references = new List<MetadataReference>(IntegrationTestBase.GetSharedReferences());
        var have = new HashSet<string>(
            references.Select(r => Path.GetFileName(r.Display ?? string.Empty)),
            StringComparer.OrdinalIgnoreCase);

        var binDir = Path.GetDirectoryName(SharpyCoreReference.Location)!;
        foreach (var dll in Directory.EnumerateFiles(binDir, "*.dll"))
        {
            if (!have.Add(Path.GetFileName(dll)))
                continue;
            try
            {
                references.Add(MetadataReference.CreateFromFile(dll));
            }
            catch
            {
                // Not a managed assembly (e.g. a native SQLite dll) — skip it.
            }
        }

        return references;
    }

    /// <summary>Mirrors <c>FileBasedIntegrationTestsBase.FindEntryPoint</c> for multi-file fixtures.</summary>
    private static string FindEntryPoint(string projectDir)
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

    private static IReadOnlyList<(string? Path, CompilationUnitSyntax Unit)> Single(CompilationUnitSyntax unit) =>
        new (string? Path, CompilationUnitSyntax Unit)[] { (null, unit) };

    private static string Label(string? path) =>
        string.IsNullOrEmpty(path) ? "<unknown>" : Path.GetFileName(path);

    /// <summary>Wraps a type in <c>class C { &lt;type&gt; f; }</c> so it can be compiled; <c>NormalizeWhitespace</c>
    /// supplies the trivia that keeps the printed text tokenizable without changing structure.</summary>
    private static CompilationUnitSyntax MinimalFieldUnit(TypeSyntax fieldType)
    {
        var field = FieldDeclaration(
            VariableDeclaration(fieldType)
                .WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier("f")))));

        var cls = ClassDeclaration("C")
            .WithMembers(SingletonList<MemberDeclarationSyntax>(field));

        return CompilationUnit()
            .WithMembers(SingletonList<MemberDeclarationSyntax>(cls))
            .NormalizeWhitespace();
    }

    // ---------------------------------------------------------------------------------------------
    // Capturing emitter factory: records each emitter-built CompilationUnitSyntax before the compiler
    // reparses it, without altering production code generation.
    // ---------------------------------------------------------------------------------------------

    private sealed class CapturingEmitterFactory : ICodeEmitterFactory
    {
        private readonly RoslynEmitterFactory _inner = new();

        public List<(string? Path, CompilationUnitSyntax Unit)> Captured { get; } = new();

        public ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default) =>
            new CapturingEmitter(_inner.Create(context, cancellationToken), context.SourceFilePath, Captured);

        private sealed class CapturingEmitter : ICodeEmitter
        {
            private readonly ICodeEmitter _inner;
            private readonly string? _sourceFilePath;
            private readonly List<(string? Path, CompilationUnitSyntax Unit)> _sink;

            public CapturingEmitter(ICodeEmitter inner, string? sourceFilePath,
                List<(string? Path, CompilationUnitSyntax Unit)> sink)
            {
                _inner = inner;
                _sourceFilePath = sourceFilePath;
                _sink = sink;
            }

            public CompilationUnitSyntax GenerateCompilationUnit(Module module)
            {
                var unit = _inner.GenerateCompilationUnit(module);
                _sink.Add((_sourceFilePath, unit));
                return unit;
            }
        }
    }
}
