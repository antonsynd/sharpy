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
/// Verifies the production <c>property_observers</c> gate registered in
/// <see cref="GatedConstructRegistry.All"/> (#416): an auto-property carrying a
/// <c>before_set</c>/<c>after_set</c> observer reports SPY0331 unless the feature is enabled,
/// and a property without observers never matches.
/// </summary>
public class PropertyObserverGatingTests
{
    private const string WithObserver =
        "class C:\n    property health: int\n        before_set(new_value):\n            assert new_value >= 0\n";

    private const string WithoutObserver =
        "class C:\n    property health: int = 100\n";

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
    public void Observer_WithoutFeature_ReportsSpy0331()
    {
        var diags = RunRealGate(WithObserver, FeatureFlags.None);

        var diag = Assert.Single(diags);
        diag.Code.Should().Be(DiagnosticCodes.Semantic.FeatureNotEnabled);
        diag.Severity.Should().Be(CompilerDiagnosticSeverity.Error);
        diag.Message.Should().Contain("property observers");
        diag.Message.Should().Contain("requires experimental feature 'property_observers'");
        diag.Message.Should().Contain("--enable-feature=property_observers");
    }

    [Fact]
    public void Observer_ParserScoped_MessageOmitsFutureImport()
    {
        // property_observers is Parser-scoped syntax, so `from __future__ import` cannot enable
        // it and the diagnostic must not suggest it.
        Assert.Single(RunRealGate(WithObserver, FeatureFlags.None))
            .Message.Should().NotContain("from __future__ import");
    }

    [Fact]
    public void Observer_WithFeatureEnabled_ReportsNothing()
    {
        var features = FeatureFlags.None.Enable("property_observers");

        RunRealGate(WithObserver, features).Should().BeEmpty();
    }

    [Fact]
    public void PlainAutoProperty_WithoutObservers_NeverMatches()
    {
        RunRealGate(WithoutObserver, FeatureFlags.None).Should().BeEmpty();
    }

    [Fact]
    public void PropertyObservers_IsAKnownParserScopedFeature()
    {
        FeatureFlags.KnownFeatures.Should().ContainKey("property_observers");
        FeatureFlags.KnownFeatures["property_observers"].Scope.Should().Be(FeatureScope.Parser);
    }
}
