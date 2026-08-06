using System.IO;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
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

    // --- Arrangement ---------------------------------------------------------------------------

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
