using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The end-to-end half of <see cref="StubClassificationTableTests"/>: the one compilation shape in
/// which <c>ModuleLoader</c>'s stub classification actually governs what the user sees.
///
/// <para>For an ordinary import it does not. Since #1087 every entry point lowers the entry file
/// plus its <em>local-import closure</em> into a synthetic project and <c>ProjectCompiler</c>
/// name-resolves every unit, so the importing file sees <c>NameResolver</c>'s symbols and
/// <c>ModuleLoader</c>'s extraction is shadowed (#1267) — measured by disabling the seam outright
/// and observing that no fixture and no CLI output changed.</para>
///
/// <para><b>The escape path:</b> a project whose declared source set does not cover a module it
/// imports. <c>&lt;SourceFile Include="src/**/*.spy" /&gt;</c> with a module beside the project file
/// resolves for imports (the project directory is a module search path) but is never a compilation
/// unit — so nothing name-resolves it, and the symbols the importing file gets are
/// <c>ModuleLoader</c>'s. That makes the classification user-visible, and it is why #1258 and #1266
/// are real defects rather than latent ones: before the fixes, a <c>pass</c>-bodied imported
/// interface method silently classified concrete here, so the "does not implement" error went
/// missing and the build fell through to an SPY0908 internal error instead.</para>
/// </summary>
public class ImportedStubClassificationReachabilityTests
{
    private readonly ITestOutputHelper _output;

    public ImportedStubClassificationReachabilityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string MainImplementingNothing = @"from lib import Logger


class Broken(Logger):
    def unrelated(self) -> None:
        print(""nothing"")


def main():
    lg: Logger = Broken()
    print(""built"")
";

    private const string MainOverridingDescribe = @"from lib import Base


class Impl(Base):
    @override
    def describe(self) -> str:
        return ""impl""


def main():
    b: Base = Impl()
    print(b.describe())
";

    /// <summary>
    /// #1258 end-to-end. A <c>pass</c>-bodied interface method in a module outside the source set is
    /// abstract, so a class that does not implement it must draw SPY0325 — the same answer the
    /// <c>...</c> spelling has always produced, which is asserted alongside it so a setup that
    /// silently failed to resolve the import (or to reach interface validation at all) fails here
    /// rather than passing as "no error".
    /// </summary>
    [Theory]
    [InlineData("pass")]
    [InlineData("...")]
    [InlineData("(...)")]
    public void UnimplementedInterfaceStub_OutsideTheSourceSet_ReportsNotImplemented(string body)
    {
        var diagnostics = AnalyzeWithModuleOutsideSourceSet(
            libSource: $"interface Logger:\n    def log(self, message: str) -> None:\n        {body}\n",
            mainSource: MainImplementingNothing);

        ErrorCodes(diagnostics).Should().Contain(
            DiagnosticCodes.Semantic.InterfaceMethodNotImplemented,
            "an interface method with a `{0}` body is abstract, so 'Broken' must be reported for not "
            + "implementing it — and on this path the answer comes from "
            + "ModuleLoader.ExtractFullInterfaceSymbol (#1258)", body);
    }

    /// <summary>
    /// #1266 end-to-end, with its own control. An ellipsis-bodied member of an imported
    /// <c>@abstract</c> class is implicitly abstract, so overriding it is legal — an
    /// <em>absence</em> assertion, which is only worth anything next to the concrete-base case that
    /// must still produce SPY0248. If the arrangement broke (import unresolved, base type not
    /// bound, override validation never reached), the control fails and the pair cannot pass
    /// vacuously.
    /// </summary>
    [Theory]
    [InlineData("...")]
    [InlineData("(...)")]
    public void OverridingAnImportedAbstractClassStub_OutsideTheSourceSet_IsAccepted(string body)
    {
        var control = AnalyzeWithModuleOutsideSourceSet(
            libSource: "@abstract\nclass Base:\n    def describe(self) -> str:\n        return \"base\"\n",
            mainSource: MainOverridingDescribe);

        ErrorCodes(control).Should().Contain(
            DiagnosticCodes.Semantic.InvalidOverride,
            "control: a base method with a real body is concrete, so @override must be refused — if "
            + "this does not fire, the arrangement never reached override validation and the "
            + "assertion below would pass for the wrong reason");

        var stub = AnalyzeWithModuleOutsideSourceSet(
            libSource: $"@abstract\nclass Base:\n    def describe(self) -> str:\n        {body}\n",
            mainSource: MainOverridingDescribe);

        ErrorCodes(stub).Should().NotContain(
            DiagnosticCodes.Semantic.InvalidOverride,
            "a `{0}`-bodied member of an @abstract class is implicitly abstract, so overriding it is "
            + "legal — the same answer NameResolver gives same-file (#1266)", body);
    }

    // --- Escapes that survive a module BEING a compilation unit --------------------------------
    //
    // There were two. The plain-import one is CLOSED (#1366/#1407/#1410): a ModuleSymbol built for
    // an in-source-set file now exports the compilation's own symbols, so `lib.X` reads exactly
    // what `from lib import X` reads. The test below that used to pin it now guards its closure.
    // The aliased from-import escape survives and is still pinned as a defect.

    private const string LibInterfaceStub = @"interface Logger:
    def log(self, message: str) -> None:
        pass
";

    private const string MainAliasedFromImport = @"from lib import Logger as Log


class Broken(Log):
    def unrelated(self) -> None:
        print(""nothing"")


def main():
    lg: Log = Broken()
    print(""built"")
";

    private const string MainPlainFromImport = @"from lib import Logger


class Broken(Logger):
    def unrelated(self) -> None:
        print(""nothing"")


def main():
    lg: Logger = Broken()
    print(""built"")
";

    private const string LibClassWithProperty = @"class Config:
    _name: str

    def __init__(self, name: str):
        self._name = name

    property get name(self) -> str:
        return self._name
";

    private const string MainPlainImport = @"import lib


def main():
    c = lib.Config(""prod"")
    print(c.name)
";

    /// <summary>
    /// Escape 1 — an <em>aliased</em> from-import. <c>ProjectCompiler.Phases.cs:351-353</c> reads
    /// <c>importAlias.AsName == null ? ResolveImportSymbol(...) : symbol</c>: the alias arm skips
    /// <see cref="ProjectCompiler"/>'s "prefer the Phase 3 original" lookup entirely and binds the
    /// symbol that came out of <c>ModuleLoader</c>. So unlike the ordinary import — where #1087's
    /// synthetic project name-resolves every unit and <c>ModuleLoader</c>'s extraction is shadowed
    /// (#1267) — three characters of <c>as</c> put the extracted symbol back in charge even though
    /// <c>lib.spy</c> is a first-class compilation unit here.
    ///
    /// <para>The plain from-import of the same declaration is carried alongside as the positive
    /// control: it must bind the <em>same instance</em> the module scope holds. Without that half,
    /// "the aliased binding differs" could mean the module scope was empty, the file never compiled,
    /// or the name never bound at all.</para>
    /// </summary>
    [Fact]
    public void AliasedFromImport_BindsTheExtractedSymbol_NotTheProjectDeclaration()
    {
        var plain = AnalyzeWithLibInSourceSet(LibInterfaceStub, MainPlainFromImport);
        var aliased = AnalyzeWithLibInSourceSet(LibInterfaceStub, MainAliasedFromImport);

        // Control: the non-aliased arm goes through ResolveImportSymbol and finds the original.
        ImportedBinding(plain, "Logger").Should().BeSameAs(
            LibDeclaration(plain, "Logger"),
            "a plain from-import runs ResolveImportSymbol, which prefers the Phase 3 declaration in "
            + "the source module's scope — if this is not the same instance, the arrangement never "
            + "reached the branch the assertion below is about");

        // The escape: the alias arm never calls ResolveImportSymbol.
        ImportedBinding(aliased, "Log").Should().NotBeSameAs(
            LibDeclaration(aliased, "Logger"),
            "`as` short-circuits ResolveImportSymbol (Phases.cs:351-353), so the alias binds the "
            + "ModuleLoader-extracted symbol even though lib.spy is a compilation unit (#1267)");

        // ...and the behavior that rides on it: the extracted symbol's classification governs.
        ErrorCodes(aliased.Diagnostics).Should().Contain(
            DiagnosticCodes.Semantic.InterfaceMethodNotImplemented,
            "the aliased interface's `pass`-bodied method is abstract, so 'Broken' must be reported "
            + "for not implementing it — on this path that answer comes from "
            + "ModuleLoader.ExtractFullInterfaceSymbol (#1258)");
    }

    /// <summary>
    /// Escape 2, CLOSED — a plain <c>import lib</c>. The <see cref="ModuleSymbol"/> built for an
    /// in-source-set file used to take its <c>Exports</c> straight from
    /// <c>moduleInfo.ExportedSymbols</c>, so every <c>lib.X</c> read answered with a
    /// <c>ModuleLoader</c>-extracted symbol even though <c>lib.spy</c> was itself name-resolved as
    /// a compilation unit. This test pinned that as an escape; it now guards its closure.
    ///
    /// <para>Why the closure mattered: an extraction is a SEPARATE object from the declaration, and
    /// only the declaration ever gets <c>BaseType</c>/<c>Interfaces</c> materialised. Every fact the
    /// twin lacked was a defect on the qualified spelling alone — inherited members invisible
    /// (#1366), assignment/argument/constraint relations empty (#1407), identity comparisons
    /// missing so <c>isinstance</c> silently tested the wrong type (#1410).</para>
    ///
    /// <para>The behavior half is retained as the arrangement's own control: <c>c.name</c> must
    /// still resolve through the module-qualified type, so "the exported symbol is the same
    /// instance" cannot pass on a module scope that never bound anything.</para>
    /// </summary>
    [Fact]
    public void PlainImport_ModuleExportsAreTheProjectDeclarations_NotExtractionCopies()
    {
        var result = AnalyzeWithLibInSourceSet(LibClassWithProperty, MainPlainImport);

        var declared = LibDeclaration(result, "Config");
        var module = ImportedBinding(result, "lib").Should().BeOfType<ModuleSymbol>(
            "`import lib` binds a ModuleSymbol in the importing file's module scope").Subject;

        module.Exports.TryGetValue("Config", out var exported).Should().BeTrue(
            "the module's export name set still comes from moduleInfo.ExportedSymbols, which must "
            + "carry 'Config'");
        exported.Should().BeSameAs(
            declared,
            "an in-source-set module exports THIS compilation's symbols, so `lib.Config` and "
            + "`from lib import Config` name one object — a separate instance here is the "
            + "extraction copy whose base chain nothing materialises (#1366, #1407, #1410)");
        module.Exports.TryGetType("Config", out var exportedType).Should().BeTrue(
            "the types-only view must be substituted too, since annotation-position resolution "
            + "consults it first (#1092)");
        exportedType.Should().BeSameAs(declared);

        // Control on the arrangement: reading a function-style property through the qualified
        // spelling must still analyze cleanly, or "same instance" could pass on an empty scope.
        ErrorCodes(result.Diagnostics).Should().BeEmpty(
            "reading a function-style property off a plainly-imported type must analyze cleanly");
        ((TypeSymbol)exported!).Properties.Should().Contain(
            p => p.Name == "name",
            "the exported TypeSymbol itself must carry the property — an empty Properties list with "
            + "no diagnostic would mean the member resolved somewhere other than this export");
    }

    /// <summary>
    /// The same closure stated where it is user-visible rather than structural: a member INHERITED
    /// from a base class resolves through a module-qualified reference. This is #1366's shape, and
    /// it is carried here next to its own control — the subclass's OWN member, which resolved
    /// through the extraction copy all along — so a run in which the import simply failed cannot
    /// pass as "no error".
    /// </summary>
    [Fact]
    public void PlainImport_InheritedMember_ResolvesThroughTheQualifiedSpelling()
    {
        var result = AnalyzeWithLibInSourceSet(
            libSource: "class Base:\n    def greet(self) -> str:\n        return \"hi\"\n"
                + "\n\nclass Child(Base):\n    def describe(self) -> str:\n        return \"child\"\n",
            mainSource: "import lib\n\n\ndef main():\n    print(lib.Child().describe())\n"
                + "    print(lib.Child().greet())\n");

        ErrorCodes(result.Diagnostics).Should().BeEmpty(
            "`lib.Child().greet()` reaches Base through the qualified spelling; the extraction copy "
            + "this used to resolve to never had its BaseType materialised, so only the INHERITED "
            + "member went missing (SPY0203) while `describe()` on the same line resolved (#1366)");
    }

    // --- Arrangement ---------------------------------------------------------------------------

    /// <summary>
    /// The ordinary multi-file arrangement: <c>lib.spy</c> and <c>main.spy</c> are both compilation
    /// units under <c>src/</c>. Asserts that, so a test about an escape from the source set cannot
    /// be confused with one about an escape that survives <em>inside</em> it.
    /// </summary>
    private ProjectAnalysisResult AnalyzeWithLibInSourceSet(string libSource, string mainSource)
    {
        using var helper = new Helpers.ProjectCompilationHelper(_output)
            .WithRootNamespace("StubEscapeInSourceSet")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");

        helper.AddSourceFile("lib.spy", libSource);
        helper.AddSourceFile("main.spy", mainSource);
        helper.CreateProjectFile();

        var config = ProjectFileParser.Load(
            Path.Combine(helper.ProjectDirectory, "StubEscapeInSourceSet.spyproj"));

        config.SourceFiles.Should().Contain(
            f => Path.GetFileName(f) == "lib.spy",
            "these tests are about escapes that survive the module being a compilation unit; if "
            + "lib.spy fell out of the source set they would silently become copies of the "
            + "outside-the-source-set tests above");

        var result = new CompilerApi().AnalyzeProject(config);
        foreach (var diag in result.Diagnostics.GetAll())
            _output.WriteLine($"  {diag.Code} {diag.Message}");

        return result;
    }

    /// <summary>
    /// The symbol an import bound in <c>main.spy</c>'s module scope. Imports register in the
    /// <em>importing</em> file's scope (<c>ResolveImports</c> enters it per unit), not the global
    /// one, so a global lookup would find nothing and every comparison below would be vacuous.
    /// </summary>
    private static Symbol ImportedBinding(ProjectAnalysisResult result, string name)
        => ModuleScopeOf(result, "main.spy").Lookup(name, searchParent: false)
            ?? throw new InvalidOperationException(
                $"arrangement failed: '{name}' is not bound in main.spy's module scope — the import "
                + "never registered anything, so there is nothing to compare");

    /// <summary>The Phase 3 declaration <c>NameResolver</c> put in <c>lib.spy</c>'s module scope.</summary>
    private static Symbol LibDeclaration(ProjectAnalysisResult result, string name)
        => ModuleScopeOf(result, "lib.spy").Lookup(name, searchParent: false)
            ?? throw new InvalidOperationException(
                $"arrangement failed: '{name}' was never declared in lib.spy's module scope — "
                + "NameResolver did not run over the module, so 'the import bound something else' "
                + "would be true for the wrong reason");

    private static Scope ModuleScopeOf(ProjectAnalysisResult result, string fileName)
    {
        var table = result.ProjectModel.GlobalSymbols
            ?? throw new InvalidOperationException("arrangement failed: analysis produced no symbol table");
        var unit = result.ProjectModel.Units.Values
            .SingleOrDefault(u => Path.GetFileName(u.FilePath) == fileName)
            ?? throw new InvalidOperationException(
                $"arrangement failed: '{fileName}' is not a compilation unit — units were "
                + string.Join(", ", result.ProjectModel.Units.Values.Select(u => Path.GetFileName(u.FilePath))));

        return table.GetModuleScope(unit.ModulePath)
            ?? throw new InvalidOperationException(
                $"arrangement failed: no module scope for '{unit.ModulePath}'");
    }

    /// <summary>
    /// Builds a project whose sources are <c>src/**/*.spy</c> and drops <c>lib.spy</c> beside the
    /// project file, outside that pattern, then analyzes (no codegen — the module has no compilation
    /// unit, so emission would fail on the missing namespace and drown the semantic answer).
    ///
    /// <para>Asserts the escape actually happened: if <c>lib.spy</c> ever became a source file, this
    /// would silently measure the ordinary closure path instead, and every cell here would be
    /// testing NameResolver.</para>
    /// </summary>
    private DiagnosticBag AnalyzeWithModuleOutsideSourceSet(string libSource, string mainSource)
    {
        using var helper = new Helpers.ProjectCompilationHelper(_output)
            .WithRootNamespace("StubEscapePath")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");

        helper.AddSourceFile("main.spy", mainSource);
        File.WriteAllText(Path.Combine(helper.ProjectDirectory, "lib.spy"), libSource);
        helper.CreateProjectFile();

        var config = ProjectFileParser.Load(
            Path.Combine(helper.ProjectDirectory, "StubEscapePath.spyproj"));

        config.SourceFiles.Should().NotContain(
            f => Path.GetFileName(f) == "lib.spy",
            "the point of this arrangement is that lib.spy is imported but NOT a compilation unit; "
            + "if it became one, NameResolver would resolve it and ModuleLoader's classification "
            + "would be shadowed again (#1267)");
        config.SourceFiles.Should().ContainSingle(
            f => Path.GetFileName(f) == "main.spy", "the entry file must be a compilation unit");

        var result = new CompilerApi().AnalyzeProject(config);
        foreach (var diag in result.Diagnostics.GetAll())
            _output.WriteLine($"  {diag.Code} {diag.Message}");

        return result.Diagnostics;
    }

    private static string[] ErrorCodes(DiagnosticBag diagnostics)
        => diagnostics.GetErrors().Select(d => d.Code).Where(c => c != null).ToArray()!;
}
