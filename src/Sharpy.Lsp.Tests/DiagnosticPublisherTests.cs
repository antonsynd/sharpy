using FluentAssertions;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class DiagnosticPublisherTests
{
    [Theory]
    [InlineData(CompilerDiagnosticSeverity.Error, DiagnosticSeverity.Error)]
    [InlineData(CompilerDiagnosticSeverity.Warning, DiagnosticSeverity.Warning)]
    [InlineData(CompilerDiagnosticSeverity.Info, DiagnosticSeverity.Information)]
    [InlineData(CompilerDiagnosticSeverity.Hint, DiagnosticSeverity.Hint)]
    public void ConvertSeverity_MapsAllLevels(CompilerDiagnosticSeverity input, DiagnosticSeverity expected)
    {
        DiagnosticPublisher.ConvertSeverity(input).Should().Be(expected);
    }

    [Fact]
    public void ConvertDiagnostic_SetsMessageAndSource()
    {
        var diag = new CompilerDiagnostic(
            Message: "unexpected token",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 5,
            Column: 10,
            Code: "SPY0100"
        );

        var result = DiagnosticPublisher.ConvertDiagnostic(diag, null);

        result.Message.Should().Be("unexpected token");
        result.Source.Should().Be("sharpy");
    }

    [Fact]
    public void ConvertDiagnostic_NoProvenance_KeepsPlainSourceAndNullData()
    {
        // A diagnostic with no producer and an Unknown phase must render identically to
        // before this feature: plain "sharpy" source and no data payload.
        var diag = new CompilerDiagnostic(
            Message: "unexpected token",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 1,
            Column: 1,
            Code: "SPY0100");

        var result = DiagnosticPublisher.ConvertDiagnostic(diag, null);

        result.Source.Should().Be("sharpy");
        result.Data.Should().BeNull();
    }

    [Fact]
    public void ConvertDiagnostic_FoldsProducerIntoSource()
    {
        var data = new Dictionary<string, string>
        {
            [DiagnosticBag.ProducerDataKey] = "ProtocolValidator",
        };
        var diag = new CompilerDiagnostic(
            Message: "Protocol not satisfied",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 1,
            Column: 1,
            Code: "SPY0500",
            Phase: CompilerPhase.Validation,
            Data: data);

        var result = DiagnosticPublisher.ConvertDiagnostic(diag, null);

        result.Source.Should().Be("sharpy:ProtocolValidator");
    }

    [Fact]
    public void ConvertDiagnostic_PublishesPhaseInData()
    {
        var diag = new CompilerDiagnostic(
            Message: "Type mismatch",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 1,
            Column: 1,
            Code: "SPY0201",
            Phase: CompilerPhase.TypeChecking);

        var result = DiagnosticPublisher.ConvertDiagnostic(diag, null);

        result.Source.Should().Be("sharpy");
        result.Data.Should().NotBeNull();
        var payload = (JObject)result.Data!;
        payload[DiagnosticPublisher.PhaseDataKey]!.Value<string>().Should().Be("TypeChecking");
    }

    [Fact]
    public void ConvertDiagnostic_PublishesProducerAndPhaseTogether()
    {
        var data = new Dictionary<string, string>
        {
            [DiagnosticBag.ProducerDataKey] = "ProtocolValidator",
        };
        var diag = new CompilerDiagnostic(
            Message: "Protocol not satisfied",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 1,
            Column: 1,
            Code: "SPY0500",
            Phase: CompilerPhase.Validation,
            Data: data);

        var result = DiagnosticPublisher.ConvertDiagnostic(diag, null);

        var payload = (JObject)result.Data!;
        payload[DiagnosticBag.ProducerDataKey]!.Value<string>().Should().Be("ProtocolValidator");
        payload[DiagnosticPublisher.PhaseDataKey]!.Value<string>().Should().Be("Validation");
    }

    [Fact]
    public void ConvertDiagnostic_ConvertsPositionTo0Based()
    {
        var diag = new CompilerDiagnostic(
            Message: "error",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 3,
            Column: 7
        );

        var result = DiagnosticPublisher.ConvertDiagnostic(diag, null);

        // Line 3, Column 7 (1-based) → Line 2, Character 6 (0-based)
        result.Range.Start.Line.Should().Be(2);
        result.Range.Start.Character.Should().Be(6);
    }

    [Fact]
    public void ConvertDiagnostics_HandlesEmptyList()
    {
        var result = DiagnosticPublisher.ConvertDiagnostics(
            Array.Empty<CompilerDiagnostic>(), null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ConvertDiagnostics_ConvertsMultiple()
    {
        var diagnostics = new[]
        {
            new CompilerDiagnostic("err1", CompilerDiagnosticSeverity.Error, Line: 1, Column: 1),
            new CompilerDiagnostic("warn1", CompilerDiagnosticSeverity.Warning, Line: 2, Column: 5),
        };

        var result = DiagnosticPublisher.ConvertDiagnostics(diagnostics, null);

        result.Should().HaveCount(2);
        result[0].Severity.Should().Be(DiagnosticSeverity.Error);
        result[1].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void GetDiagnosticTags_TagsUnnecessaryStaticDecoratorAsUnnecessary()
    {
        var diag = new CompilerDiagnostic(
            Message: "@static is unnecessary",
            Severity: CompilerDiagnosticSeverity.Hint,
            Code: DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint);

        var tags = DiagnosticPublisher.GetDiagnosticTags(diag);

        tags.Should().NotBeNull();
        tags!.Should().Contain(DiagnosticTag.Unnecessary);
    }

    [Fact]
    public void GetDiagnosticTags_DoesNotTagInformationalHints()
    {
        // SPY0476 is informational about behavioral differences, not redundant code.
        var diag = new CompilerDiagnostic(
            Message: "negative tuple index",
            Severity: CompilerDiagnosticSeverity.Hint,
            Code: DiagnosticCodes.Validation.NegativeTupleIndexHint);

        var tags = DiagnosticPublisher.GetDiagnosticTags(diag);

        tags.Should().BeNull();
    }

    [Fact]
    public void GetDiagnosticTags_DoesNotTagErrorsOrWarnings()
    {
        var diag = new CompilerDiagnostic(
            Message: "error",
            Severity: CompilerDiagnosticSeverity.Error,
            Code: DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint);

        DiagnosticPublisher.GetDiagnosticTags(diag).Should().BeNull();
    }

    [Theory]
    [InlineData("SPY0470", true)]
    [InlineData("SPY0477", true)]
    [InlineData("SPY0478", true)]
    [InlineData("SPY0479", true)]   // last code in the band (retired, but in range)
    // SPY0480-SPY0489 is the validation-WARNING overflow band, not part of the transition band.
    // These four cells are the discriminating half of #1466: every one of them answered `true`
    // before the range was narrowed to 479.
    [InlineData("SPY0480", false)]  // MustUseValueDiscarded (#1022)
    [InlineData("SPY0484", false)]  // BuiltinRebornByExplicitImport (#1324)
    [InlineData("SPY0489", false)]  // reserved tail of the warning-overflow band
    [InlineData("SPY0469", false)]
    [InlineData("SPY0490", false)]
    [InlineData("SPY0100", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("INVALID", false)]
    public void IsTransitionHintCode_RecognizesRange(string? code, bool expected)
    {
        DiagnosticPublisher.IsTransitionHintCode(code).Should().Be(expected);
    }

    [Fact]
    public void ConvertDiagnostics_KeepsValidationWarningOverflowBandWhenTransitionHintsDisabled()
    {
        // The user-visible contract of #1466: with transition hints off, the editor and the
        // command line still agree about SPY0480-0484.
        //
        // Be honest about what this pins. It is green under EITHER single regression on its own —
        // widening the range back to 489 leaves these five untouched because they emit at Warning,
        // and dropping the severity conjunct leaves them untouched because 0480-0484 is now out of
        // range. It fails only if both regress together. The discriminating guard for the range
        // itself is IsTransitionHintCode_RecognizesRange's 0480/0484/0489 cells, which flip the
        // moment TransitionHintRangeEnd goes back to 489; this test is the end-to-end backstop
        // behind them, kept because it states the contract in the terms a user would.
        var configuration = new LspConfiguration();
        configuration.UpdateFrom(JToken.Parse("""{"transitionHints":{"enabled":false}}"""));

        // Named through the constants, not literals, so renumbering one of these into the
        // transition band fails here rather than silently re-opening #1466.
        var overflowWarnings = new[]
        {
            DiagnosticCodes.Validation.MustUseValueDiscarded,
            DiagnosticCodes.Validation.UnusedSuppression,
            DiagnosticCodes.Validation.InvalidSuppressionCode,
            DiagnosticCodes.Validation.BuiltinNameShadowedInValuePosition,
            DiagnosticCodes.Validation.BuiltinRebornByExplicitImport,
        };

        var diagnostics = overflowWarnings
            .Select((code, i) => new CompilerDiagnostic(
                "overflow-band warning", CompilerDiagnosticSeverity.Warning,
                Line: i + 1, Column: 1, Code: code))
            .Append(new CompilerDiagnostic(
                "genuine transition hint", CompilerDiagnosticSeverity.Hint,
                Line: 6, Column: 1,
                Code: DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint))
            .ToArray();

        var result = DiagnosticPublisher.ConvertDiagnostics(diagnostics, null, configuration);

        result.Select(d => d.Code?.String).Should().BeEquivalentTo(overflowWarnings,
            "the editor and the command line must agree about SPY0480-0484; only the genuine "
            + "transition hint (SPY0477) is advisory enough to hide");
    }

    [Fact]
    public void ConvertDiagnostics_FiltersTransitionHintsWhenDisabled()
    {
        var configuration = new LspConfiguration();
        configuration.UpdateFrom(JToken.Parse("""{"transitionHints":{"enabled":false}}"""));

        var diagnostics = new[]
        {
            new CompilerDiagnostic("err1", CompilerDiagnosticSeverity.Error, Line: 1, Column: 1, Code: "SPY0100"),
            new CompilerDiagnostic("warn1", CompilerDiagnosticSeverity.Warning, Line: 2, Column: 1, Code: "SPY0450"),
            new CompilerDiagnostic("hint1", CompilerDiagnosticSeverity.Hint, Line: 3, Column: 1, Code: "SPY0476"),
            new CompilerDiagnostic("hint2", CompilerDiagnosticSeverity.Hint, Line: 4, Column: 1, Code: "SPY0477"),
        };

        var result = DiagnosticPublisher.ConvertDiagnostics(diagnostics, null, configuration);

        result.Should().HaveCount(2);
        result.Select(d => d.Severity).Should().NotContain(DiagnosticSeverity.Hint);
    }

    [Fact]
    public void ConvertDiagnostics_KeepsTransitionHintsByDefault()
    {
        var configuration = new LspConfiguration();

        var diagnostics = new[]
        {
            new CompilerDiagnostic("hint1", CompilerDiagnosticSeverity.Hint, Line: 1, Column: 1, Code: "SPY0476"),
            new CompilerDiagnostic("hint2", CompilerDiagnosticSeverity.Hint, Line: 2, Column: 1, Code: "SPY0477"),
        };

        var result = DiagnosticPublisher.ConvertDiagnostics(diagnostics, null, configuration);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void ConvertDiagnostics_DoesNotFilterNonTransitionHintsWhenDisabled()
    {
        var configuration = new LspConfiguration();
        configuration.UpdateFrom(JToken.Parse("""{"transitionHints":{"enabled":false}}"""));

        // A Hint-severity diagnostic outside the SPY0470-SPY0489 range — should not be filtered.
        var diagnostics = new[]
        {
            new CompilerDiagnostic("style hint", CompilerDiagnosticSeverity.Hint, Line: 1, Column: 1, Code: "SPY0999"),
        };

        var result = DiagnosticPublisher.ConvertDiagnostics(diagnostics, null, configuration);

        result.Should().HaveCount(1);
    }

    // sharpy.lsp.maxNumberOfProblems (#1165) — contributed since the extension's first release,
    // read by nobody until now.

    private static CompilerDiagnostic[] MixedSeverityDiagnostics() =>
    [
        new("hint", CompilerDiagnosticSeverity.Hint, Line: 1, Column: 1, Code: "SPY0476"),
        new("warning", CompilerDiagnosticSeverity.Warning, Line: 2, Column: 1, Code: "SPY0451"),
        new("error one", CompilerDiagnosticSeverity.Error, Line: 3, Column: 1, Code: "SPY0201"),
        new("error two", CompilerDiagnosticSeverity.Error, Line: 4, Column: 1, Code: "SPY0202"),
    ];

    private static LspConfiguration ConfigurationWithCap(string capJson)
    {
        var configuration = new LspConfiguration();
        configuration.UpdateFrom(JToken.Parse("{\"lsp\":{\"maxNumberOfProblems\":" + capJson + "}}"));
        return configuration;
    }

    [Fact]
    public void ConvertDiagnostics_NoCapConfigured_PublishesEverything()
    {
        var result = DiagnosticPublisher.ConvertDiagnostics(
            MixedSeverityDiagnostics(), null, new LspConfiguration());

        result.Should().HaveCount(4);
    }

    [Fact]
    public void ConvertDiagnostics_UnderCap_PublishesEverything()
    {
        var result = DiagnosticPublisher.ConvertDiagnostics(
            MixedSeverityDiagnostics(), null, ConfigurationWithCap("10"));

        result.Should().HaveCount(4);
    }

    [Fact]
    public void ConvertDiagnostics_OverCap_DropsTheLeastSevere()
    {
        // The compiler emits in phase order, and hint-producing validators run before
        // error-producing ones — a positional truncation would hide the errors.
        var result = DiagnosticPublisher.ConvertDiagnostics(
            MixedSeverityDiagnostics(), null, ConfigurationWithCap("2"));

        result.Should().HaveCount(2);
        result.Select(d => d.Message).Should().Equal("error one", "error two");
    }

    [Fact]
    public void ConvertDiagnostics_OverCap_KeepsOriginalOrder()
    {
        var result = DiagnosticPublisher.ConvertDiagnostics(
            MixedSeverityDiagnostics(), null, ConfigurationWithCap("3"));

        result.Select(d => d.Message).Should().Equal(new[] { "warning", "error one", "error two" },
            "the survivors keep the order the editor already showed them in");
    }

    [Fact]
    public void ConvertDiagnostics_ZeroCap_PublishesNothing()
    {
        var result = DiagnosticPublisher.ConvertDiagnostics(
            MixedSeverityDiagnostics(), null, ConfigurationWithCap("0"));

        result.Should().BeEmpty("zero is a real cap, not a spelling of unlimited");
    }

    [Fact]
    public void ConvertDiagnostics_NegativeCap_PublishesEverything()
    {
        var result = DiagnosticPublisher.ConvertDiagnostics(
            MixedSeverityDiagnostics(), null, ConfigurationWithCap("-1"));

        result.Should().HaveCount(4, "a negative maximum cannot mean 'show fewer than none'");
    }

    [Fact]
    public void ConvertDiagnostics_CapAppliesAfterTransitionHintFiltering()
    {
        // Filtering first means a suppressed hint does not consume a slot the user wanted
        // spent on a real problem.
        var configuration = new LspConfiguration();
        configuration.UpdateFrom(JToken.Parse(
            """{"transitionHints":{"enabled":false},"lsp":{"maxNumberOfProblems":2}}"""));

        var result = DiagnosticPublisher.ConvertDiagnostics(
            MixedSeverityDiagnostics(), null, configuration);

        result.Select(d => d.Message).Should().Equal("error one", "error two");
    }

    [Fact]
    public void ConvertDiagnostic_PublishesRelatedLocationsAsRelatedInformation()
    {
        // SPY0522 names TWO declarations. The second is the diagnostic's own position; the first
        // used to exist only as "(line N)" prose inside the message, which an editor cannot
        // navigate to. It now also rides along structurally (#1388).
        var diag = new CompilerDiagnostic(
            Message: "Name collision: 'h' and 'H' (line 4) both compile to 'H'.",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 6,
            Column: 1,
            FilePath: "/tmp/grid.spy",
            Code: "SPY0522",
            RelatedLocations: new[]
            {
                new DiagnosticRelatedLocation("'H' is first declared here", 4, 10, "/tmp/grid.spy")
            });

        var result = DiagnosticPublisher.ConvertDiagnostic(diag, null);

        result.RelatedInformation.Should().NotBeNull();
        var related = result.RelatedInformation!.Single();
        related.Message.Should().Be("'H' is first declared here");
        related.Location.Range.Start.Line.Should().Be(3, "LSP positions are zero-based");
        related.Location.Range.Start.Character.Should().Be(9, "so is the character offset");
        related.Location.Uri.GetFileSystemPath().Should().Be("/tmp/grid.spy");
    }

    [Fact]
    public void ConvertDiagnostic_NoRelatedLocations_LeavesRelatedInformationNull()
    {
        // The overwhelming majority of diagnostics name one position. Publishing an empty
        // container instead of null would put a stray "0 related" affordance on every one.
        var diag = new CompilerDiagnostic(
            Message: "unexpected token",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 1,
            Column: 1,
            Code: "SPY0100");

        var result = DiagnosticPublisher.ConvertDiagnostic(diag, null);

        result.RelatedInformation.Should().BeNull();
    }

    [Fact]
    public void ConvertDiagnostic_GeneratedOriginAndRelatedLocations_PublishesBoth()
    {
        // The generated-origin branch owned RelatedInformation outright before #1388; both
        // sources must now coexist rather than one clobbering the other.
        var diag = new CompilerDiagnostic(
            Message: "Name collision",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 6,
            Column: 1,
            FilePath: "/tmp/grid.spy",
            Code: "SPY0522",
            RelatedLocations: new[]
            {
                new DiagnosticRelatedLocation("'H' is first declared here", 4, 10, "/tmp/grid.spy")
            });
        var origin = new CompilerDiagnostic(
            Message: "origin",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 2,
            Column: 3,
            FilePath: "Foo:Bar");

        var result = DiagnosticPublisher.ConvertDiagnostic(diag, null, origin);

        result.RelatedInformation!.Select(r => r.Message).Should().Equal(
            "In generated source Foo:Bar", "'H' is first declared here");
    }
}
