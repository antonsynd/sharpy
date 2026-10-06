using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Lsp.Handlers;
using Sharpy.Lsp.Tests.Conformance;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// The indent-only check and the funnel's second arm (P22e decision 8, #2168): the full and range
/// fallbacks — they run only when the document does not lex or parse — reach the client only through
/// <see cref="FormattingEdits.CheckedIndentOnly"/>. Each clause of
/// <see cref="FormattingFallback.IndentOnlyPreserved"/> has a refused and an accepted twin; the
/// Current State cells are asserted BY DIRECTION: the unchecked candidate is the damaged text the
/// handler returned @ ec673074f (it returned the candidate as it stands here, unchecked), and the
/// handler now returns no edits.
/// </summary>
public sealed class FormattingFallbackTests : IDisposable
{
    /// <summary>D5u: a triple-quoted string not closed yet.</summary>
    private const string D5u = "def main():\n    s = \"\"\"\n        key: value\n";

    /// <summary>D12: an 8-space document cut inside a bracket — it does not parse.</summary>
    private const string D12 =
        "def main():\n        if False:\n                print(1)\n                print(2)\n        x = (\n";

    /// <summary>D19: a docstring opened in <c>f</c> ABOVE <c>g</c>'s multi-line string.</summary>
    private const string D19 =
        "def f() -> int:\n    \"\"\"\n    return 1\ndef g() -> str:\n    s = \"\"\"\n        key: value\n    \"\"\"\n    return s\n";

    /// <summary>D20f: the lexer aborts INSIDE an f-string closed in the text (<c>Unmatched '}'</c>).</summary>
    private const string D20f =
        "def main():\n    s = f\"\"\"\n        }\n        key: value\n    \"\"\"\n    print(s)\n";

    /// <summary>D20b: the lexer aborts INSIDE a byte string closed in the text (non-ASCII).</summary>
    private const string D20b =
        "def main():\n    s = b\"\"\"\n        é\n        key: value\n    \"\"\"\n    print(s)\n";

    /// <summary>D21: an S3 cut, 8 spaces per level, the block opener the last line.</summary>
    private const string D21 = "def main():\n        a = 1\n        if a == 1:\n";

    /// <summary>A 2-space body: the document the fallback exists to repair (SPY0013 on both lines).</summary>
    private const string TwoSpace = "def main():\n  x: int = 1\n  print(x)\n";

    /// <summary>An S3 cut below a closed multi-line string: lexes, does not parse, literal line 2.</summary>
    private const string ClosedLiteral =
        "def main():\n    s = \"\"\"\n        key: value\n    \"\"\"\n        if s:\n";

    private readonly LspFormattingDriver _driver = new();

    public void Dispose() => _driver.Dispose();

    private static string WithLine(string text, int line, string newLine)
    {
        var lines = text.Split('\n');
        lines[line] = newLine;
        return string.Join("\n", lines);
    }

    private static LspRange LineRange(string text, int line) =>
        new(new Position(line, 0), new Position(line, text.Split('\n')[line].Length));

    /// <summary>The range fallback's unchecked candidate for one line, applied.</summary>
    private static string RangeCandidate(string text, int line) =>
        LspFormattingDriver.ApplyStrict(text, SharpyRangeFormattingHandler.ComputeIndentOnlyRangeEdits(text, line, line));

    // ---- clause 1: the same number of lines ----

    [Fact]
    public void LineCount_ALostOrGainedLine_IsRefused()
    {
        FormattingFallback.IndentOnlyPreserved(TwoSpace, "def main():\n    x: int = 1\n").Should().BeFalse();
        FormattingFallback.IndentOnlyPreserved(TwoSpace, TwoSpace + "\n").Should().BeFalse();
        FormattingFallback.IndentOnlyPreserved(TwoSpace, "def main():\n    x: int = 1\n    print(x)\n").Should().BeTrue();
    }

    // ---- clause 2: each line's content after its leading whitespace ----

    [Fact]
    public void Content_AChangedLine_IsRefused_AWhitespaceOnlyLineMayBecomeEmpty()
    {
        FormattingFallback.IndentOnlyPreserved(TwoSpace, "def main():\n    x: int = 2\n    print(x)\n").Should().BeFalse();
        FormattingFallback.IndentOnlyPreserved(TwoSpace, "def main():\n    x: int = 1  \n    print(x)\n").Should().BeFalse();

        const string blank = "def main():\n  x: int = 1\n   \n  print(x)\n";
        FormattingFallback.IndentOnlyPreserved(blank, "def main():\n    x: int = 1\n\n    print(x)\n").Should().BeTrue();
    }

    // ---- clause 3: literal lines byte-identical ----

    [Fact]
    public void Literal_AReindentedStringLine_IsRefused_ACodeLineBesideItIsNot()
    {
        IndentationService.BuildIndentMap(ClosedLiteral).LiteralLines.Should().BeEquivalentTo(new[] { 3, 4 });
        FormattingFallback.IndentOnlyPreserved(ClosedLiteral, WithLine(ClosedLiteral, 2, "    key: value")).Should().BeFalse();
        FormattingFallback.IndentOnlyPreserved(ClosedLiteral, WithLine(ClosedLiteral, 4, "    if s:")).Should().BeTrue();
    }

    // ---- clause 4: the level of every logical-line-start line ----

    [Fact]
    public void Level_OneLineOfAn8SpaceBlockReindented_IsRefused_TheWholeReindentIsNot()
    {
        // Cell 16: `print(2)` at 8 spaces leaves the `if False:` block.
        FormattingFallback.IndentOnlyPreserved(D12, WithLine(D12, 3, "        print(2)")).Should().BeFalse();
        FormattingFallback.IndentOnlyPreserved(D12, FormattingFallback.ReindentDocument(D12)).Should().BeTrue();
    }

    // ---- clause 5: no more lexer indentation diagnostics ----

    [Fact]
    public void Diagnostics_ADedentBetweenTwoWidths_IsRefused_ARepairIsNot()
    {
        // Cell 21: the map gives `if a == 1:` level 1 before and after; the lexer reports SPY0014 after.
        var damaged = WithLine(D21, 2, "    if a == 1:");
        IndentationService.BuildIndentMap(damaged).LineIndent[3].Should().Be(IndentationService.BuildIndentMap(D21).LineIndent[3]);
        FormattingFallback.IndentOnlyPreserved(D21, damaged).Should().BeFalse();

        // A partial re-indent of the 2-space body: SPY0013 2 → 1.
        FormattingFallback.IndentOnlyPreserved(TwoSpace, "def main():\n    x: int = 1\n  print(x)\n").Should().BeTrue();
    }

    // ---- the funnel ----

    [Fact]
    public void Funnel_AParseableSource_IsDecidedByTheNet()
    {
        // D11 parses: moving `print(2)` out of `if False:` changes the program (cell 10/11's shape).
        const string d11 = "def main():\n        if False:\n                print(1)\n                print(2)\n";
        var moved = new[] { new TextEdit { Range = LineRange(d11, 3), NewText = "        print(2)" } };
        FormattingEdits.CheckedIndentOnly(d11, moved).Should().BeEmpty();

        var same = new[] { new TextEdit { Range = LineRange(d11, 3), NewText = "                print(2)" } };
        FormattingEdits.CheckedIndentOnly(d11, same).Should().HaveCount(1);
    }

    [Fact]
    public void Funnel_AnUnparseableRepair_KeepsItsEdits()
    {
        var (edits, applied) = _driver.Full(TwoSpace + "    if x:\n");
        edits.Should().NotBeEmpty();
        applied.Should().Be("def main():\n    x: int = 1\n    print(x)\n    if x:\n");
    }

    // ---- Current State cells, by direction ----

    [Fact]
    public void Cell14_FullFallback_OpenString_IsUntouched()
    {
        FormattingFallback.ReindentDocument(D5u).Should().Be(WithLine(D5u, 2, "    key: value"));
        _driver.Full(D5u).Edits.Should().BeEmpty();
    }

    [Fact]
    public void Cell15_RangeFallback_OpenString_IsUntouched()
    {
        RangeCandidate(D5u, 2).Should().Be(WithLine(D5u, 2, "    key: value"));
        _driver.Range(D5u, LineRange(D5u, 2)).Edits.Should().BeEmpty();
    }

    [Fact]
    public void Cell16_RangeFallback_OneLineOfAn8SpaceBlock_GetsNoEdits()
    {
        RangeCandidate(D12, 3).Should().Be(WithLine(D12, 3, "        print(2)"));
        _driver.Range(D12, LineRange(D12, 3)).Edits.Should().BeEmpty();
    }

    [Fact]
    public void Cell19_LostString_FullAndRangeFallbacks_GetNoEdits()
    {
        var damaged = WithLine(D19, 5, "    key: value");
        FormattingFallback.ReindentDocument(D19).Should().Be(damaged);
        RangeCandidate(D19, 5).Should().Be(damaged);

        _driver.Full(D19).Edits.Should().BeEmpty();
        _driver.Range(D19, LineRange(D19, 5)).Edits.Should().BeEmpty();
    }

    [Theory]
    [InlineData(D20f, "    }")]
    [InlineData(D20b, "    é")]
    public void Cell20_AbortInsideALiteral_FullFallback_GetsNoEdits(string document, string firstInterior)
    {
        FormattingFallback.ReindentDocument(document)
            .Should().Be(WithLine(WithLine(document, 2, firstInterior), 3, "    key: value"));
        _driver.Full(document).Edits.Should().BeEmpty();
    }

    [Fact]
    public void Cell21_RangeFallback_DedentBetweenTwoWidths_GetsNoEdits()
    {
        RangeCandidate(D21, 2).Should().Be(WithLine(D21, 2, "    if a == 1:"));
        _driver.Range(D21, LineRange(D21, 2)).Edits.Should().BeEmpty();
    }

    /// <summary>
    /// The restriction decision 7 (option a) accepts: on an S4 document the lines ABOVE the open
    /// literal were re-indented correctly and are not edited now — the lexer's literal map of the
    /// whole document is unknown once a multi-line literal is lost.
    /// </summary>
    [Fact]
    public void S4_LinesAboveTheOpenLiteral_AreNotEdited()
    {
        const string s4 = "def main():\n        x = 1\n        s = \"\"\"\n        key: value\n";
        FormattingFallback.ReindentDocument(s4).Should().StartWith("def main():\n    x = 1\n");
        _driver.Full(s4).Edits.Should().BeEmpty();
        _driver.Range(s4, LineRange(s4, 1)).Edits.Should().BeEmpty();
    }
}
