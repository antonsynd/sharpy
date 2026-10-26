using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.Compiler.Text;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Verifies <see cref="FeatureGateChecker"/> reports SPY0331 for constructs gated behind
/// a disabled experimental feature, and stays silent when the feature is enabled — whether
/// via the compilation-wide flags or a per-file <c>from __future__ import</c>.
/// </summary>
/// <remarks>
/// No real gated construct ships yet, so these tests inject a test-only construct that gates
/// the <c>pass</c> statement behind the <c>__test_feature</c> flag (Semantic scope). The
/// production registry (<see cref="GatedConstructRegistry.All"/>) is empty and exercised via
/// its short-circuit path.
/// </remarks>
public class FeatureGateCheckerTests
{
    // Gates the 'pass' statement behind __test_feature (a real, Semantic-scoped known feature).
    private static readonly IReadOnlyList<GatedConstruct> GatePass = new[]
    {
        new GatedConstruct(
            "__test_feature",
            "the 'pass' statement",
            n => n is PassStatement),
    };

    private static Module ParseModule(string source)
    {
        var lexResult = FileCompilationPipeline.Lex(new SourceText(source, "test.spy"), NullLogger.Instance);
        var parseResult = FileCompilationPipeline.Parse(lexResult.Tokens, NullLogger.Instance);
        parseResult.HasErrors.Should().BeFalse("test source should parse cleanly");
        return parseResult.Module!;
    }

    private static IReadOnlyList<CompilerDiagnostic> RunChecker(
        string source, FeatureFlags features, IReadOnlyList<GatedConstruct> gates)
    {
        var module = ParseModule(source);
        var diagnostics = new DiagnosticBag();
        new FeatureGateChecker(diagnostics, features, "test.spy", gates).Check(module);
        return diagnostics.GetAll();
    }

    [Fact]
    public void GatedConstruct_WithoutFlag_ReportsSpy0331()
    {
        var diags = RunChecker("def main() -> None:\n    pass\n", FeatureFlags.None, GatePass);

        var diag = Assert.Single(diags);
        diag.Code.Should().Be(DiagnosticCodes.Semantic.FeatureNotEnabled);
        diag.Severity.Should().Be(CompilerDiagnosticSeverity.Error);
        diag.Message.Should().Contain("the 'pass' statement");
        diag.Message.Should().Contain("requires experimental feature '__test_feature'");
        diag.Message.Should().Contain("--enable-feature=__test_feature");
        diag.Message.Should().Contain("<Features>__test_feature</Features>");
    }

    [Fact]
    public void GatedConstruct_WithCliStyleFlag_ReportsNothing()
    {
        var features = FeatureFlags.None.Enable("__test_feature");

        var diags = RunChecker("def main() -> None:\n    pass\n", features, GatePass);

        diags.Should().BeEmpty();
    }

    [Fact]
    public void SemanticScopedGate_Message_SuggestsFutureImport()
    {
        var diags = RunChecker("def main() -> None:\n    pass\n", FeatureFlags.None, GatePass);

        var diag = Assert.Single(diags);
        // __test_feature is Semantic-scoped, so the future-import path is a valid alternative.
        diag.Message.Should().Contain("from __future__ import __test_feature");
    }

    [Fact]
    public void NoGatedConstructs_IsNoOp()
    {
        // The production registry is empty; a plain module produces no gating diagnostics.
        var diags = RunChecker("def main() -> None:\n    pass\n", FeatureFlags.None, GatedConstructRegistry.All);

        diags.Should().BeEmpty();
    }

    [Fact]
    public void UngatedModule_WithGateConfigured_ReportsNothing()
    {
        // A module that never uses the gated construct is clean even without the flag.
        var diags = RunChecker("def main() -> None:\n    x: int = 1\n", FeatureFlags.None, GatePass);

        diags.Should().BeEmpty();
    }

    [Fact]
    public void FailableCast_Graduated_ProducesNoSpy0331_WithProductionRegistry()
    {
        // #1096: `as?`/`as!` graduated — the production registry no longer carries a failable_cast
        // gate, so an ungated as-cast is accepted (no SPY0331). Regression guard against re-adding
        // the gate; the five ungated `.error` twins that used to assert SPY0331 here were deleted.
        var diags = RunChecker(
            "def main() -> None:\n    y = x as? int\n",
            FeatureFlags.None,
            GatedConstructRegistry.All);

        diags.Where(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled).Should().BeEmpty();
    }

    /// <summary>
    /// Compiles <paramref name="source"/> as a one-file project on the live route (ProjectCompiler:
    /// Phase 4 records the file's <c>from __future__ import</c> features) and returns the effective
    /// feature set the semantic phase uses for that file, with the file's own parsed AST.
    /// </summary>
    private static (FeatureFlags Features, Module Ast, IReadOnlyList<CompilerDiagnostic> Diagnostics)
        CompileLive(string source)
    {
        using var helper = new ProjectCompilationHelper()
            .WithRootNamespace("FeatureGateLive").WithOutputType("exe").WithEntryPoint("main.spy");
        helper.AddSourceFile("main.spy", source, isEntryPoint: true);
        var result = helper.Compile();
        var unit = result.ProjectModel!.Units.Values.Single(u => Path.GetFileName(u.FilePath) == "main.spy");
        var features = result.ImportResolver!.GetEffectiveFeatures(result.EffectiveFeatures, unit.FilePath);
        return (features, unit.Ast!, result.Diagnostics.GetAll());
    }

    [Fact]
    public void GatedConstruct_EnabledViaFutureImport_ReportsNothing()
    {
        // End-to-end union path: `from __future__ import __test_feature` must satisfy the gate
        // exactly as a compilation-wide flag would — the features come from the compile route
        // itself, and the test-only gate (the production registry is empty) runs against them.
        var (features, ast, _) = CompileLive(
            "from __future__ import __test_feature\n\n\ndef main() -> None:\n    pass\n");
        features.IsEnabled("__test_feature").Should().BeTrue("the future import should enable it");

        var diagnostics = new DiagnosticBag();
        new FeatureGateChecker(diagnostics, features, "main.spy", GatePass).Check(ast);
        diagnostics.GetAll().Where(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled)
            .Should().BeEmpty();

        // Positive control: the same construct without the future import is gated.
        var (ungated, ungatedAst, _) = CompileLive("\n\n\ndef main() -> None:\n    pass\n");
        ungated.IsEnabled("__test_feature").Should().BeFalse();
        var controlDiagnostics = new DiagnosticBag();
        new FeatureGateChecker(controlDiagnostics, ungated, "main.spy", GatePass).Check(ungatedAst);
        controlDiagnostics.GetAll().Should().ContainSingle(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled);
    }

    [Fact]
    public void UnknownFutureFeature_StillRejected_Spy0330()
    {
        // Regression guard: SPY0330 behavior for an unknown future feature is unchanged and
        // is distinct from the new SPY0331 gate diagnostic.
        var (_, _, diagnostics) = CompileLive(
            "from __future__ import not_a_real_feature\n\n\ndef main() -> None:\n    pass\n");

        diagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.UnknownFutureFeature);
        diagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled);
    }
}
