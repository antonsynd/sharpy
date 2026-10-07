using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Formatting;
using Sharpy.Lsp.Tests.Conformance;
using Sharpy.TestInfrastructure.Formatting;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// One line model across the formatting routes (P22e Phase 4 Task 5, #2168): <c>\r\n</c>, <c>\n</c> and a
/// lone <c>\r</c> each end a line — in the lexer, in <c>LineDiff</c>, in the LSP spec, and so in every
/// handler and in the funnel's check. Each pin asserts the APPLIED text (<see cref="LspFormattingDriver"/>).
/// The documents are the standalone verifier's cells @ 88345108f (LC3, A1, A3, A5, A6), where a
/// <c>\n</c>-only split made the server check one text and the client apply another.
/// </summary>
public sealed class LineBreakFormattingTests : IDisposable
{
    private readonly LspFormattingDriver _driver = new();

    public void Dispose() => _driver.Dispose();

    private static LspRange R(int startLine, int startChar, int endLine, int endChar)
        => new(new Position(startLine, startChar), new Position(endLine, endChar));

    // ---- Format Document on a document that parses ----

    /// <summary>
    /// LC3: one lone <c>\r</c> in a document that parses. The client applies <c>Format</c>'s output — one
    /// <c>print(x)</c>, the program unchanged — not the formatted text followed by the old line 2.
    /// </summary>
    [Fact]
    public void LC3_FormatDocument_ALoneCrInAParseableDocument_AppliesFormatsOutput()
    {
        const string text = "def main():\r    x   =  1\n    print(x)\n";
        var applied = _driver.Full(text).Applied;

        applied.Should().Be("def main():\n    x = 1\n    print(x)\n");
        applied.Should().Be(FormatterService.Format(text).FormattedText);
        FormatterService.CheckApplied(text, applied, out var parses).Should().BeNull();
        parses.Should().BeTrue();
    }

    /// <summary>A1: every break a lone <c>\r</c> — the whole-document edit ends at the end of the document's last line.</summary>
    [Fact]
    public void A1_FormatDocument_EveryBreakALoneCr_AppliesFormatsOutput()
    {
        const string d1 = "def helper() -> int:\n    return 2\ndef main():\n    x   =   helper()\n    print(x)\n";
        var text = FormatterTwins.Cr(d1);

        _driver.Full(text).Applied.Should().Be("def helper() -> int:\n    return 2\n\n\ndef main():\n    x = helper()\n    print(x)\n");
    }

    // ---- the full fallback keeps each line's own break ----

    /// <summary>
    /// A6: an unparseable CRLF document already indented right gets no edits — the fallback no longer
    /// rewrites every <c>\r\n</c> as <c>\n</c>. Positive control: its 2-space twin is re-indented, CRLF kept.
    /// </summary>
    [Fact]
    public void A6_FullFallback_CrlfDocument_KeepsCrlf()
    {
        const string indented = "def main():\r\n    x = 1\r\n    y = (\r\n";
        _driver.Full(indented).Edits.Should().BeEmpty();

        _driver.Full("def main():\r\n  x = 1\r\n  y = (\r\n").Applied.Should().Be(indented);
    }

    /// <summary>A3: the full fallback on a lone-<c>\r</c> document re-indents each line and keeps its <c>\r</c>; on a mixed one, each line's own break.</summary>
    [Fact]
    public void A3_FullFallback_LoneCrDocuments_KeepEachLinesBreak()
    {
        _driver.Full("def main():\r  x = 1\r  y = (\r").Applied.Should().Be("def main():\r    x = 1\r    y = (\r");
        _driver.Full("def main():\r  x = 1\n  y = (\n").Applied.Should().Be("def main():\r    x = 1\n    y = (\n");
    }

    // ---- the range fallback ----

    /// <summary>A3: the range fallback re-indents the selected lines of a lone-<c>\r</c> (and a mixed) document — no line dropped or duplicated.</summary>
    [Fact]
    public void A3_RangeFallback_LoneCrDocuments_ReindentsTheSelectedLines()
    {
        _driver.Range("def main():\r  x = 1\r  y = (\r", R(1, 0, 2, 7)).Applied.Should().Be("def main():\r    x = 1\r    y = (\r");
        _driver.Range("def main():\r  x = 1\n  y = (\n", R(0, 0, 2, 7)).Applied.Should().Be("def main():\r    x = 1\n    y = (\n");
    }

    // ---- format on type ----

    /// <summary>A5: on-type at line 2 re-indents line 2 — the line the client counts — on a lone-<c>\r</c> and on a mixed document.</summary>
    [Fact]
    public void A5_OnType_LoneCrDocuments_ReindentsTheRequestedLine()
    {
        _driver.OnType("def main():\r    if True:\r            print(1)\r    print(2)\r", 2).Applied
            .Should().Be("def main():\r    if True:\r        print(1)\r    print(2)\r");
        _driver.OnType("def main():\r    if True:\n            print(1)\n    print(2)\n", 2).Applied
            .Should().Be("def main():\r    if True:\n        print(1)\n    print(2)\n");
    }
}
