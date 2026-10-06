using FluentAssertions;
using Sharpy.Compiler.Formatting;
using Sharpy.Lsp.Handlers;
using Sharpy.Lsp.Tests.Conformance;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using Sweep = Sharpy.Lsp.Tests.Conformance.FormattingRouteParitySweepTests;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// <see cref="FormattingEdits.ToTextEdits"/> is the edit form of <see cref="LineDiff.Apply"/> (P22e decision 4,
/// #2168): a client applying the edits strictly (<see cref="LspFormattingDriver.ApplyStrict"/>) gets exactly
/// the text <see cref="LineDiff.Apply"/> gives — line breaks included.
/// </summary>
public class FormattingEditsTests
{
    private static void AssertAgrees(string source, IReadOnlyList<LineHunk> hunks)
    {
        var edits = FormattingEdits.ToTextEdits(source, hunks);
        edits.Should().HaveCount(hunks.Count);
        LspFormattingDriver.ApplyStrict(source, edits).Should().Be(LineDiff.Apply(source, hunks),
            $"the edits of [{string.Join(", ", hunks.Select(h => $"[{h.Start},{h.End})+{h.NewLines.Count}"))}] must apply as LineDiff.Apply does");
    }

    /// <summary>Every hunk set, each single hunk, and every other hunk, of the hunks <paramref name="source"/> → <paramref name="target"/>.</summary>
    private static void AssertAgreesOnSubsets(string source, string target)
    {
        var hunks = LineDiff.Hunks(source, target);
        AssertAgrees(source, hunks);
        foreach (var hunk in hunks)
            AssertAgrees(source, new[] { hunk });
        AssertAgrees(source, hunks.Where((_, i) => i % 2 == 0).ToList());
    }

    [Theory]
    [InlineData("a\nb", "x\na\nb")]               // insert at the start
    [InlineData("a\nb", "a\nx\ny\nb")]            // insert in the middle
    [InlineData("a\nb", "a\nb\n")]                // insert the missing final line break
    [InlineData("a\nb", "a\nb\nc\nd")]            // insert after the last line
    [InlineData("", "a\n")]                       // insert into the empty document
    [InlineData("x\na\nb", "a\nb")]               // delete at the start
    [InlineData("a\nx\ny\nb", "a\nb")]            // delete in the middle
    [InlineData("a\nb\nc", "a\nb")]               // delete the last line
    [InlineData("a\nb\n", "a\nb")]                // delete the final line break
    [InlineData("a\nb", "")]                      // delete everything
    [InlineData("a\nb\nc", "x\nb\nc")]            // replace at the start
    [InlineData("a\nb\nc", "a\nx\ny\nc")]         // replace one line with two
    [InlineData("a\nb\nc\nd", "a\nx\nd")]         // replace two lines with one
    [InlineData("a\nb\nc", "a\nb\nx\n")]          // replace the last line and add the break
    [InlineData("a\r\nb\r\nc\r\n", "x\na\nb\ny\n")] // CRLF: inserts write CRLF
    [InlineData("a\r\nb\r\nc", "a\nx\nc\n")]      // CRLF, no final line break
    [InlineData("a\r\nb\r\nc\r\n", "a\nc")]       // CRLF: delete through the end
    [InlineData("a\rb\nc", "a\nx\nb\nc\nd")]      // mixed: the first break is a lone CR
    [InlineData("é😀\nb", "é😀\nx\n")]            // UTF-16 positions
    public void HandCases_ApplyAsLineDiffApply(string source, string target)
        => AssertAgreesOnSubsets(source, target);

    [Fact]
    public void OneToOneReplacement_KeepsTheBareLineShape()
    {
        var source = "def foo():\n        x: int = 1\n    return x\n";
        var edits = FormattingEdits.ToTextEdits(source, new[] { new LineHunk(1, 2, new[] { "    x: int = 1" }) });

        edits.Should().ContainSingle();
        edits[0].Range.Should().Be(new LspRange(1, 0, 1, 18));
        edits[0].NewText.Should().Be("    x: int = 1");
    }

    [Theory]
    [MemberData(nameof(Sweep.CorpusNames), MemberType = typeof(Sweep))]
    public void CorpusTwins_ApplyAsLineDiffApply(string stem)
    {
        var source = Sweep.Corpus.Value.Corpus[stem].Source;
        foreach (var twin in Sweep.Twins)
        {
            var q = Sweep.TwinText(source, twin);
            var formatted = FormatterService.Format(q).FormattedText;
            AssertAgreesOnSubsets(q, formatted);
            AssertAgreesOnSubsets(formatted, q);
        }
    }
}
