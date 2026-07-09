using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Verifies that <see cref="CompilerOptions.Features"/> is threaded into the semantic
/// phase, so semantic/codegen-scoped feature gates can observe them (C1).
/// </summary>
public class FeatureFlagsThreadingTests
{
    private static FeatureFlags RunTypeCheckWithFeatures(FeatureFlags features)
    {
        const string source = "def main() -> None:\n    x: int = 1\n";

        var lexResult = FileCompilationPipeline.Lex(
            new SourceText(source, "test.spy"), NullLogger.Instance, features: features);
        var parseResult = FileCompilationPipeline.Parse(
            lexResult.Tokens, NullLogger.Instance, features: features);

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        var pipeline = new FileCompilationPipeline(symbolTable, semanticInfo, semanticBinding, NullLogger.Instance);
        pipeline.ResolveNames(parseResult.Module!);

        var diagnostics = new DiagnosticBag();
        var typeCheckResult = pipeline.TypeCheck(
            parseResult.Module!, "test.spy", isEntryPoint: false, maxErrors: 100, diagnostics,
            features: features);

        return typeCheckResult.TypeChecker.Features;
    }

    [Fact]
    public void TypeChecker_ObservesEnabledFeature_WhenThreadedFromOptions()
    {
        var features = FeatureFlags.None.Enable("__test_feature");

        var observed = RunTypeCheckWithFeatures(features);

        observed.IsEnabled("__test_feature").Should().BeTrue();
    }

    [Fact]
    public void TypeChecker_DoesNotObserveFeature_WhenNoneEnabled()
    {
        var observed = RunTypeCheckWithFeatures(FeatureFlags.None);

        observed.IsEnabled("__test_feature").Should().BeFalse();
    }

    [Fact]
    public void Parser_CarriesFeatures()
    {
        var features = FeatureFlags.None.Enable("__test_feature");
        var lexResult = FileCompilationPipeline.Lex(
            new SourceText("def main() -> None:\n    pass\n", "test.spy"), NullLogger.Instance, features: features);
        var tokenList = new System.Collections.Generic.List<global::Sharpy.Compiler.Lexer.Token>(lexResult.Tokens);

        var parser = new global::Sharpy.Compiler.Parser.Parser(tokenList, NullLogger.Instance, features: features);

        parser.Features.IsEnabled("__test_feature").Should().BeTrue();
    }
}
