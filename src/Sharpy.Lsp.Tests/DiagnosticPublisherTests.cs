using FluentAssertions;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;
using Sharpy.Lsp;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

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

        result.Message!.String.Should().Be("unexpected token");
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
        result.Select(d => d.Message!.String).Should().Equal("error one", "error two");
    }

    [Fact]
    public void ConvertDiagnostics_OverCap_KeepsOriginalOrder()
    {
        var result = DiagnosticPublisher.ConvertDiagnostics(
            MixedSeverityDiagnostics(), null, ConfigurationWithCap("3"));

        result.Select(d => d.Message!.String).Should().Equal(new[] { "warning", "error one", "error two" },
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

        result.Select(d => d.Message!.String).Should().Equal("error one", "error two");
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

    // ---------------------------------------------------------------------------------------
    // Publication partitions by file (P53 D4, #2242). An analysis result may hold diagnostics of
    // other files — the single-file door returns its synthetic project's whole bag, import
    // closure included. Each diagnostic is published on the file it belongs to and positioned
    // against THAT file's text; positioning lib.spy's SPY0204 against main.spy's buffer threw
    // ArgumentOutOfRangeException and dropped main.spy's whole notification.
    // ---------------------------------------------------------------------------------------

    private static readonly string PartitionDir =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sharpy-publisher-partition");
    private static readonly string MainPath = System.IO.Path.Combine(PartitionDir, "main.spy");
    private static readonly string LibPath = System.IO.Path.Combine(PartitionDir, "lib.spy");
    private static readonly string MainUri = new Uri(MainPath).ToString();
    private static readonly string LibUri = new Uri(LibPath).ToString();

    // 17 characters: any span starting past offset 17 cannot be positioned against this text.
    private const string MainSource = "x: int = 1\ny = x\n";

    // lib.spy: twelve lines, the duplicate declaration on line 12 at offset well past main's end.
    private static readonly string LibSource =
        string.Concat(Enumerable.Range(1, 11).Select(i => $"v{i}: int = {i}\n")) + "class Widget:\n    pass\n";

    private static CompilerDiagnostic LibDuplicateClass(string? filePath)
    {
        var start = LibSource.IndexOf("Widget", StringComparison.Ordinal);
        return new CompilerDiagnostic(
            Message: "Duplicate declaration of 'Widget'",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 12,
            Column: 7,
            FilePath: filePath,
            Code: "SPY0204",
            Span: new TextSpan(start, "Widget".Length));
    }

    private static SemanticResult ResultOf(params CompilerDiagnostic[] diagnostics)
        => new() { Diagnostics = diagnostics };

    [Fact]
    public void BuildPublications_SiblingFileDiagnostic_IsPublishedOnItsOwnUri_AndNothingThrows()
    {
        LibSource.IndexOf("Widget", StringComparison.Ordinal).Should().BeGreaterThan(MainSource.Length,
            "the cell needs a sibling span that main.spy's text cannot resolve");
        var own = new CompilerDiagnostic(
            "own", CompilerDiagnosticSeverity.Error, Line: 2, Column: 1, Code: "SPY0200", Span: new TextSpan(11, 1));
        // The same sibling file spelled two ways (a path and a file URI) is one file.
        var sibling = LibDuplicateClass(LibPath);
        var siblingAsUri = LibDuplicateClass(LibUri) with { Message = "second" };

        IReadOnlyList<PublishDiagnosticsParams>? publications = null;
        var act = () => publications = DiagnosticPublisher.BuildPublications(
            MainUri, ResultOf(own, sibling, siblingAsUri), new SourceText(MainSource, MainPath),
            sourceTextFor: null, configuration: null);

        act.Should().NotThrow("a sibling's span must never be positioned against the open buffer's text");
        publications!.Select(p => p.Uri.GetFileSystemPath()).Should().Equal(MainPath, LibPath);

        var mainDiagnostics = publications![0].Diagnostics.ToList();
        mainDiagnostics.Select(d => d.Code?.String).Should().Equal("SPY0200");
        mainDiagnostics[0].Range.Should().Be(new LspRange(1, 0, 1, 1), "the document's own span resolves against its own text");

        var libDiagnostics = publications[1].Diagnostics.ToList();
        libDiagnostics.Select(d => d.Message!.String).Should().Equal("Duplicate declaration of 'Widget'", "second");
        libDiagnostics.Should().AllSatisfy(d => d.Range.Should().Be(new LspRange(11, 6, 11, 6),
            "with no text for lib.spy the recorded line/column is the position, zero-width"));
    }

    [Fact]
    public void BuildPublications_SiblingFileDiagnostic_IsPositionedAgainstTheResolvedSiblingText()
    {
        var requested = new List<string>();
        SourceText? Resolve(string uri)
        {
            requested.Add(uri);
            return string.Equals(uri, LibUri, StringComparison.Ordinal) ? new SourceText(LibSource, LibPath) : null;
        }

        var publications = DiagnosticPublisher.BuildPublications(
            MainUri, ResultOf(LibDuplicateClass(LibPath)), new SourceText(MainSource, MainPath), Resolve, null);

        requested.Should().Equal(new[] { LibUri }, "the resolver is asked for the sibling only, by the URI it is published on");
        publications.Select(p => p.Uri.GetFileSystemPath()).Should().Equal(MainPath, LibPath);
        publications[0].Diagnostics.Should().BeEmpty("main.spy owns none of them — and the empty notification clears it");
        publications[1].Diagnostics.Single().Range.Should().Be(new LspRange(11, 6, 11, 12),
            "the span is resolved against lib.spy's own text");
    }

    [Fact]
    public void BuildPublications_SameFileDiagnostic_InRangeSpan_YieldsTheSpansExactRange()
    {
        // Positive control for the cell above: the identical diagnostic stamped with the
        // document's own path, its span inside the document's text, converts to the span's range.
        var start = MainSource.IndexOf('y');
        var diag = LibDuplicateClass(MainPath) with { Line = 2, Column = 1, Span = new TextSpan(start, 1) };

        var publications = DiagnosticPublisher.BuildPublications(
            MainUri, ResultOf(diag), new SourceText(MainSource, MainPath), sourceTextFor: null, configuration: null);

        publications.Should().ContainSingle();
        publications[0].Uri.GetFileSystemPath().Should().Be(MainPath);
        publications[0].Diagnostics.Single().Range.Should().Be(new LspRange(1, 0, 1, 1));
    }

    [Fact]
    public void BuildPublications_NullFilePath_LandsOnTheRequestingDocument()
    {
        var diag = new CompilerDiagnostic(
            "pathless", CompilerDiagnosticSeverity.Error, Line: 1, Column: 1, FilePath: null, Code: "SPY0200",
            Span: new TextSpan(0, 1));

        var publications = DiagnosticPublisher.BuildPublications(
            MainUri, ResultOf(diag), new SourceText(MainSource, MainPath), sourceTextFor: null, configuration: null);

        publications.Should().ContainSingle();
        publications[0].Uri.GetFileSystemPath().Should().Be(MainPath);
        publications[0].Diagnostics.Single().Range.Should().Be(new LspRange(0, 0, 0, 1));
    }

    [Fact]
    public void BuildPublications_NoDiagnostics_StillPublishesAnEmptySetOnTheDocument()
    {
        var publications = DiagnosticPublisher.BuildPublications(
            MainUri, ResultOf(), new SourceText(MainSource, MainPath), sourceTextFor: null, configuration: null);

        publications.Should().ContainSingle();
        publications[0].Uri.GetFileSystemPath().Should().Be(MainPath);
        publications[0].Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void BuildPublications_GeneratedSourceDiagnostic_IsReroutedBeforePartitioning()
    {
        // The reroute stamps the document URI, so a generated-source diagnostic lands on the
        // document at its trigger decorator — never in a partition of its own, and its span
        // (an offset into the GENERATED text) is never positioned against the document.
        var source =
            "class GenerateEquals:\n" +
            "    def generate(self, context: int) -> int:\n" +
            "        return 0\n" +
            "\n" +
            "@[GenerateEquals]\n" +
            "class Point:\n" +
            "    x: int = 0\n";
        var analysis = new CompilerApi().Analyze(source);
        var generator = analysis.SymbolTable!.LookupType("GenerateEquals");
        generator.Should().NotBeNull();
        generator!.IsSourceGenerator = true;
        var point = analysis.Ast!.Body.OfType<ClassDef>().Single(c => c.Name == "Point");
        var trigger = point.Decorators.Single(d => d.Name == "GenerateEquals");
        analysis.SemanticInfo!.AddGeneratorBinding(point, generator, trigger);

        var generated = new CompilerDiagnostic(
            "type mismatch in generated code", CompilerDiagnosticSeverity.Error, Line: 3, Column: 1,
            FilePath: "<generated:GenerateEquals:Point>", Code: "SPY0552", Span: new TextSpan(5000, 3));
        var unrerouted = generated with { FilePath = "<generated:NoSuchGenerator:Point>", Message = "unbound" };

        var publications = DiagnosticPublisher.BuildPublications(
            MainUri, analysis with { Diagnostics = new[] { generated, unrerouted } },
            new SourceText(source, MainPath), sourceTextFor: null, configuration: null);

        publications.Should().ContainSingle("both generated diagnostics belong to the requesting document");
        publications[0].Uri.GetFileSystemPath().Should().Be(MainPath);
        var diagnostics = publications[0].Diagnostics.ToList();
        diagnostics.Select(d => d.Message!.String).Should().Equal("type mismatch in generated code", "unbound");
        diagnostics[0].Range.Start.Should().Be(new Position(trigger.LineStart - 1, trigger.ColumnStart - 1));
        diagnostics[0].RelatedInformation!.Single().Message.Should().Contain("<generated:GenerateEquals:Point>");
        diagnostics[1].Range.Should().Be(new LspRange(2, 0, 2, 0),
            "an unrerouted generated position indexes the generated text: line/column only");
    }

    [Fact]
    public void BuildPublications_ProblemCap_AppliesPerNotification()
    {
        var configuration = new LspConfiguration();
        configuration.UpdateFrom(JToken.Parse("""{"lsp":{"maxNumberOfProblems":1}}"""));
        var diagnostics = new[]
        {
            new CompilerDiagnostic("own-1", CompilerDiagnosticSeverity.Error, Line: 1, Column: 1),
            new CompilerDiagnostic("own-2", CompilerDiagnosticSeverity.Error, Line: 2, Column: 1),
            LibDuplicateClass(LibPath),
            LibDuplicateClass(LibPath) with { Message = "lib-2" },
        };

        var publications = DiagnosticPublisher.BuildPublications(
            MainUri, ResultOf(diagnostics), new SourceText(MainSource, MainPath), null, configuration);

        publications.Select(p => p.Diagnostics.Count()).Should().Equal(1, 1);
        publications[0].Diagnostics.Single().Message!.String.Should().Be("own-1");
        publications[1].Diagnostics.Single().Message!.String.Should().Be("Duplicate declaration of 'Widget'");
    }

    [Fact]
    public void ReadSourceTextFromDisk_ReadsAnExistingFile_AndAnswersNullForAnAbsentOrNonFileUri()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sharpy-publisher-disk-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var path = System.IO.Path.Combine(dir, "lib.spy");
            System.IO.File.WriteAllText(path, LibSource);

            DiagnosticPublisher.ReadSourceTextFromDisk(new Uri(path).ToString())!.ToString().Should().Be(LibSource);
            DiagnosticPublisher.ReadSourceTextFromDisk(new Uri(System.IO.Path.Combine(dir, "gone.spy")).ToString())
                .Should().BeNull("a file deleted since analysis falls back to line/column ranges");
            DiagnosticPublisher.ReadSourceTextFromDisk("untitled:Untitled-1").Should().BeNull();
        }
        finally
        {
            System.IO.Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ConvertDiagnostic_SpanOutsideTheText_FallsBackToLineColumn_InsteadOfThrowing()
    {
        // A span that starts past the end of the supplied text was recorded against another text
        // (or an older version of this one). The recorded line/column is text-independent; a
        // clamp would invent an end-of-file position the diagnostic never named.
        var diag = LibDuplicateClass(MainPath);

        Diagnostic? converted = null;
        var act = () => converted = DiagnosticPublisher.ConvertDiagnostic(diag, new SourceText(MainSource, MainPath));

        act.Should().NotThrow();
        converted!.Range.Should().Be(new LspRange(11, 6, 11, 6));
    }
}
