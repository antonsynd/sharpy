using System.Collections.Generic;
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
/// Verifies the production <c>defer</c> gate registered in
/// <see cref="GatedConstructRegistry.All"/> (#1023): ungated use of a <c>defer</c>
/// statement reports SPY0331, and enabling the <c>defer</c> feature silences it.
/// </summary>
public class DeferGatingTests
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
    public void InlineDefer_WithoutFeature_ReportsSpy0331()
    {
        var diags = RunRealGate("def main() -> None:\n    defer f.close()\n", FeatureFlags.None);

        var diag = Assert.Single(diags);
        diag.Code.Should().Be(DiagnosticCodes.Semantic.FeatureNotEnabled);
        diag.Severity.Should().Be(CompilerDiagnosticSeverity.Error);
        diag.Message.Should().Contain("the 'defer' statement");
        diag.Message.Should().Contain("requires experimental feature 'defer'");
        diag.Message.Should().Contain("--enable-feature=defer");
    }

    [Fact]
    public void BlockDefer_WithoutFeature_ReportsSpy0331()
    {
        var diags = RunRealGate("def main() -> None:\n    defer:\n        f.close()\n", FeatureFlags.None);

        var diag = Assert.Single(diags);
        diag.Code.Should().Be(DiagnosticCodes.Semantic.FeatureNotEnabled);
        diag.Message.Should().Contain("the 'defer' statement");
    }

    [Fact]
    public void Defer_ParserScoped_MessageOmitsFutureImport()
    {
        var diags = RunRealGate("def main() -> None:\n    defer f.close()\n", FeatureFlags.None);

        // defer is Parser-scoped syntax, so `from __future__ import` cannot enable it and
        // the diagnostic must not suggest it.
        Assert.Single(diags).Message.Should().NotContain("from __future__ import");
    }

    [Fact]
    public void Defer_WithFeatureEnabled_ReportsNothing()
    {
        var features = FeatureFlags.None.Enable("defer");

        RunRealGate("def main() -> None:\n    defer f.close()\n", features).Should().BeEmpty();
    }

    [Fact]
    public void Defer_IsAKnownParserScopedFeature()
    {
        FeatureFlags.KnownFeatures.Should().ContainKey("defer");
        FeatureFlags.KnownFeatures["defer"].Scope.Should().Be(FeatureScope.Parser);
    }
}
