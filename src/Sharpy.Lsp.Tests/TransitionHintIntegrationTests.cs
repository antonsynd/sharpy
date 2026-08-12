using FluentAssertions;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Integration tests that exercise the full pipeline from compiler analysis
/// through <see cref="DiagnosticPublisher"/>. Uses real source code that
/// triggers SPY0477 (unnecessary @static decorator) so the tests stay aligned
/// with the actual validator implementation.
/// </summary>
public class TransitionHintIntegrationTests
{
    private readonly CompilerApi _api = new();

    /// <summary>
    /// A class with a method that triggers SPY0477: <c>@static</c> on a
    /// method that already lacks <c>self</c>. The TransitionWarningValidator
    /// emits a Hint-severity diagnostic for this construct.
    /// </summary>
    private const string SourceWithUnnecessaryStaticDecorator = """
        class Box:
            @static
            def helper(x: int) -> int:
                return x + 1
        """;

    [Fact]
    public void Compiler_EmitsHintSeverity_ForUnnecessaryStaticDecorator()
    {
        var result = _api.Analyze(SourceWithUnnecessaryStaticDecorator);

        var hint = result.Diagnostics.Should()
            .ContainSingle(d => d.Code == DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint)
            .Subject;
        hint.Severity.Should().Be(CompilerDiagnosticSeverity.Hint);
    }

    [Fact]
    public void HintDiagnostic_AppearsAsLspHintSeverity()
    {
        var result = _api.Analyze(SourceWithUnnecessaryStaticDecorator);

        var lspDiagnostics = DiagnosticPublisher.ConvertDiagnostics(result.Diagnostics, sourceText: null);

        var lspHint = lspDiagnostics.Should()
            .ContainSingle(d =>
                d.Code.HasValue
                && d.Code.Value.IsString
                && d.Code.Value.String == DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint)
            .Subject;
        lspHint.Severity.Should().Be(DiagnosticSeverity.Hint);
    }

    [Fact]
    public void HintDiagnostic_HasUnnecessaryDiagnosticTag()
    {
        var result = _api.Analyze(SourceWithUnnecessaryStaticDecorator);

        var lspDiagnostics = DiagnosticPublisher.ConvertDiagnostics(result.Diagnostics, sourceText: null);

        var lspHint = lspDiagnostics.Single(d =>
            d.Code.HasValue
            && d.Code.Value.IsString
            && d.Code.Value.String == DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint);

        lspHint.Tags.Should().NotBeNull("SPY0477 marks redundant code and must surface DiagnosticTag.Unnecessary so editors can fade it");
        lspHint.Tags!.Should().Contain(DiagnosticTag.Unnecessary);
    }

    [Fact]
    public void HintDiagnostic_FilteredOut_WhenTransitionHintsDisabled()
    {
        var result = _api.Analyze(SourceWithUnnecessaryStaticDecorator);

        var configuration = new LspConfiguration();
        configuration.UpdateFrom(JToken.Parse("""{"transitionHints":{"enabled":false}}"""));

        var lspDiagnostics = DiagnosticPublisher.ConvertDiagnostics(
            result.Diagnostics,
            sourceText: null,
            configuration);

        lspDiagnostics.Should().NotContain(d =>
            d.Code.HasValue
            && d.Code.Value.IsString
            && d.Code.Value.String == DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint,
            "transition hints must be filtered when sharpy.transitionHints.enabled is false");
    }

    [Fact]
    public void HintDiagnostic_Preserved_WhenTransitionHintsEnabled()
    {
        var result = _api.Analyze(SourceWithUnnecessaryStaticDecorator);

        // Default configuration: transitionHints.enabled = true
        var configuration = new LspConfiguration();

        var lspDiagnostics = DiagnosticPublisher.ConvertDiagnostics(
            result.Diagnostics,
            sourceText: null,
            configuration);

        lspDiagnostics.Should().Contain(d =>
            d.Code.HasValue
            && d.Code.Value.IsString
            && d.Code.Value.String == DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint);
    }

    /// <summary>
    /// SPY0478 joins the toggle by range rather than by registration
    /// (<c>DiagnosticPublisher.IsTransitionHintCode</c>), so nothing wires it up explicitly —
    /// which is exactly why it needs pinning: a new hint silently either does or does not honour
    /// the user-facing off-switch, and only a test says which (#1394).
    /// </summary>
    [Fact]
    public void AliasedCollectionHint_HonoursTheTransitionHintsToggle()
    {
        const string source = """
            def main() -> None:
                s: set[int] = {1, 2}
                t: set[int] = s
                s |= {3}
                print(len(t))
            """;

        var result = _api.Analyze(source);

        DiagnosticPublisher.ConvertDiagnostics(result.Diagnostics, sourceText: null, new LspConfiguration())
            .Should().Contain(
                d => d.Code.HasValue && d.Code.Value.IsString
                    && d.Code.Value.String == DiagnosticCodes.Validation.AliasedCollectionAugmentedAssignmentHint,
                "the hint is published under the default configuration");

        var disabled = new LspConfiguration();
        disabled.UpdateFrom(JToken.Parse("""{"transitionHints":{"enabled":false}}"""));

        DiagnosticPublisher.ConvertDiagnostics(result.Diagnostics, sourceText: null, disabled)
            .Should().NotContain(
                d => d.Code.HasValue && d.Code.Value.IsString
                    && d.Code.Value.String == DiagnosticCodes.Validation.AliasedCollectionAugmentedAssignmentHint,
                "SPY0478 is in the transition-hint range, so the off-switch must reach it");
    }

    [Fact]
    public void NonHintDiagnostics_Unaffected_ByTransitionHintsSetting()
    {
        // Source mixes a hint (SPY0477) with a real type error.
        const string source = """
            class Box:
                @static
                def helper(x: int) -> int:
                    return x + 1

            x: int = "not an int"
            """;

        var result = _api.Analyze(source);

        var configuration = new LspConfiguration();
        configuration.UpdateFrom(JToken.Parse("""{"transitionHints":{"enabled":false}}"""));

        var lspDiagnostics = DiagnosticPublisher.ConvertDiagnostics(
            result.Diagnostics,
            sourceText: null,
            configuration);

        // Hint filtered out, but errors still present.
        lspDiagnostics.Should().NotContain(d =>
            d.Code.HasValue
            && d.Code.Value.IsString
            && d.Code.Value.String == DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint);
        lspDiagnostics.Should().Contain(d => d.Severity == DiagnosticSeverity.Error,
            "type errors must still be published even when transition hints are disabled");
    }

    [Fact]
    public void HintDiagnostic_HasCorrectRange()
    {
        var result = _api.Analyze(SourceWithUnnecessaryStaticDecorator);

        var lspDiagnostics = DiagnosticPublisher.ConvertDiagnostics(result.Diagnostics, sourceText: null);
        var lspHint = lspDiagnostics.Single(d =>
            d.Code.HasValue
            && d.Code.Value.IsString
            && d.Code.Value.String == DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint);

        // The hint points at the @static decorator on line 2 (1-based) which
        // is line 1 in 0-based LSP coordinates.
        lspHint.Range.Should().NotBeNull();
        lspHint.Range.Start.Line.Should().Be(1);
        lspHint.Range.Start.Character.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void HintDiagnostic_HasSharpySource()
    {
        var result = _api.Analyze(SourceWithUnnecessaryStaticDecorator);

        var lspDiagnostics = DiagnosticPublisher.ConvertDiagnostics(result.Diagnostics, sourceText: null);
        var lspHint = lspDiagnostics.Single(d =>
            d.Code.HasValue
            && d.Code.Value.IsString
            && d.Code.Value.String == DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint);

        // Validator-produced diagnostics now fold their producer into the LSP source so the
        // origin is visible in the editor's diagnostic origin column (#1054). The source still
        // starts with "sharpy"; the ":TransitionWarning" suffix identifies the producing validator.
        lspHint.Source.Should().Be("sharpy:TransitionWarning");
    }
}
