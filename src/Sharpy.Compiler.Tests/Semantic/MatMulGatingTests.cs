using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Verifies the production <c>matmul</c> gate registered in
/// <see cref="GatedConstructRegistry.All"/>: ungated use of <c>@</c> / <c>@=</c> reports
/// SPY0331, and enabling the <c>matmul</c> feature silences it.
/// </summary>
public class MatMulGatingTests
{
    private static Module ParseModule(string source)
    {
        var lexResult = FileCompilationPipeline.Lex(new SourceText(source, "test.spy"), NullLogger.Instance);
        var parseResult = FileCompilationPipeline.Parse(lexResult.Tokens, NullLogger.Instance);
        parseResult.HasErrors.Should().BeFalse("test source should parse cleanly");
        return parseResult.Module!;
    }

    private static IReadOnlyList<CompilerDiagnostic> RunRealGate(string source, FeatureFlags features)
    {
        var module = ParseModule(source);
        var diagnostics = new DiagnosticBag();
        // gatedConstructs omitted → uses the production GatedConstructRegistry.All.
        new FeatureGateChecker(diagnostics, features, "test.spy").Check(module);
        return diagnostics.GetAll();
    }

    [Fact]
    public void InfixMatMul_WithoutFeature_ReportsSpy0331()
    {
        var diags = RunRealGate("def main() -> None:\n    c = a @ b\n", FeatureFlags.None);

        var diag = Assert.Single(diags);
        diag.Code.Should().Be(DiagnosticCodes.Semantic.FeatureNotEnabled);
        diag.Severity.Should().Be(CompilerDiagnosticSeverity.Error);
        diag.Message.Should().Contain("the '@' matrix-multiplication operator");
        diag.Message.Should().Contain("requires experimental feature 'matmul'");
        diag.Message.Should().Contain("--enable-feature=matmul");
    }

    [Fact]
    public void InfixMatMul_ParserScoped_MessageOmitsFutureImport()
    {
        var diags = RunRealGate("def main() -> None:\n    c = a @ b\n", FeatureFlags.None);

        // matmul is Parser-scoped, so `from __future__ import` cannot enable it and the
        // diagnostic must not suggest it.
        Assert.Single(diags).Message.Should().NotContain("from __future__ import");
    }

    [Fact]
    public void AugmentedMatMul_WithoutFeature_ReportsSpy0331()
    {
        var diags = RunRealGate("def main() -> None:\n    x @= y\n", FeatureFlags.None);

        var diag = Assert.Single(diags);
        diag.Code.Should().Be(DiagnosticCodes.Semantic.FeatureNotEnabled);
        diag.Message.Should().Contain("the '@=' matrix-multiplication assignment");
    }

    [Fact]
    public void MatMul_WithFeatureEnabled_ReportsNothing()
    {
        var features = FeatureFlags.None.Enable("matmul");

        RunRealGate("def main() -> None:\n    c = a @ b\n", features).Should().BeEmpty();
        RunRealGate("def main() -> None:\n    x @= y\n", features).Should().BeEmpty();
    }

    [Fact]
    public void MatMul_IsAKnownFeature()
    {
        FeatureFlags.KnownFeatures.Should().ContainKey("matmul");
        FeatureFlags.KnownFeatures["matmul"].Scope.Should().Be(FeatureScope.Parser);
    }
}
