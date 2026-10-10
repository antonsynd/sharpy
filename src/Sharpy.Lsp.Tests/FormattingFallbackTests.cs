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
        // The code line after the closer at another width that keeps its block depth (moving it to 4
        // spaces is a re-nest: UnexpectedIndent_FullFallback_GetsNoEdits).
        FormattingFallback.IndentOnlyPreserved(ClosedLiteral, WithLine(ClosedLiteral, 4, "            if s:")).Should().BeTrue();
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

    // ---- clause 6: the lexer's block depth of every logical-line-start line on its enclosing stack ----

    /// <summary>
    /// A property-observer block (<c>experimental/property_observers_*</c>, <c>Formatting/member_line_comments</c>)
    /// cut after it at a block opener with no body: the indent map opens a block only after a line ending
    /// in <c>:</c>, so it does not see the property line open one.
    /// </summary>
    private const string Observers =
        "class Character:\n    property health: int = 100\n        before_set(new_value):\n            print(new_value)\n    def heal(self) -> None:\n";

    /// <summary>
    /// <c>strings/d_string_trailing_newline</c> with <c>"""</c> inserted as line 0: it pairs with the
    /// <c>"""</c> in line 2's comment, the later quotes re-pair, and the lexer reports nothing.
    /// </summary>
    private const string DStringRepaired =
        "\"\"\"\ndef main():\n    # A blank line before the closing \"\"\" becomes a trailing newline\n    msg: str = d\"\"\"\n        hello\n\n        \"\"\"\n    print(repr(msg))\n";

    /// <summary>
    /// An unexpected indent (<c>y</c>: the lexer pushes a level, the map does not) above a line at a
    /// width between two levels of the stack (<c>z</c>: on no enclosing level, SPY0014 — exempt).
    /// </summary>
    private const string BetweenWidths = "def main():\n    x = 1\n            y = 2\n        z = 3\n";

    [Fact]
    public void Depth_AnObserverBlockReindentedOneLevelShallow_IsRefused_A2SpaceTwinReindentedTo4IsNot()
    {
        var shallow = WithLine(Observers, 2, "    before_set(new_value):");
        IndentationService.BuildIndentMap(shallow).LineIndent[3].Should().Be(IndentationService.BuildIndentMap(Observers).LineIndent[3],
            "the map's level of the observer line is the same before and after — clause 4 alone accepts it");
        FormattingFallback.IndentOnlyPreserved(Observers, shallow).Should().BeFalse();

        const string twoSpace =
            "class Character:\n  property health: int = 100\n    before_set(new_value):\n      print(new_value)\n  def heal(self) -> None:\n";
        FormattingFallback.IndentOnlyPreserved(twoSpace, Observers).Should().BeTrue();
    }

    [Fact]
    public void Depth_ABlockReNestedByTriplesThatRePair_IsRefused()
    {
        IndentationService.BuildIndentMap(DStringRepaired).LiteralStateUnknown.Should().BeFalse("the re-pairing is lexically consistent");
        var flattened = WithLine(WithLine(DStringRepaired, 3, "msg: str = d\"\"\""), 7, "print(repr(msg))");
        FormattingFallback.IndentOnlyPreserved(DStringRepaired, flattened).Should().BeFalse();

        // Twin: the same two lines widened instead — another width, the same depth.
        var widened = WithLine(WithLine(DStringRepaired, 3, "        msg: str = d\"\"\""), 7, "        print(repr(msg))");
        FormattingFallback.IndentOnlyPreserved(DStringRepaired, widened).Should().BeTrue();
    }

    [Fact]
    public void Depth_ALineOnNoEnclosingLevel_IsExempt_ItsNeighbourIsNot()
    {
        // `z` repaired to the function's level: the line the fallback exists to repair.
        var repaired = WithLine(BetweenWidths, 3, "    z = 3");
        FormattingFallback.IndentOnlyPreserved(BetweenWidths, repaired).Should().BeTrue();

        // The same repair, and its neighbour `y` moved out of the level the lexer pushed for it: the
        // map gives `y` level 1 before and after, and the lexer reports no more than before.
        var neighbour = WithLine(repaired, 2, "    y = 2");
        IndentationService.BuildIndentMap(neighbour).LineIndent[3].Should().Be(IndentationService.BuildIndentMap(BetweenWidths).LineIndent[3]);
        IndentationService.BuildIndentMap(neighbour).IndentationDiagnostics.Should().BeLessThanOrEqualTo(IndentationService.BuildIndentMap(BetweenWidths).IndentationDiagnostics);
        FormattingFallback.IndentOnlyPreserved(BetweenWidths, neighbour).Should().BeFalse();
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
        var (edits, applied) = _driver.Full(TwoSpace + "  if x:\n");
        edits.Should().NotBeEmpty();
        applied.Should().Be("def main():\n    x: int = 1\n    print(x)\n    if x:\n");
    }

    /// <summary>
    /// The restriction clause 6 accepts (lead ruling, P22e decision 8): a line deeper than the logical
    /// line above it, where that line does not end in <c>:</c>, opens a block for the lexer — the lexer
    /// cannot tell an unexpected indent from a colon-less block opener (a property line opening its
    /// observers), so the indent-only routes leave it where it is. By direction: @ aef9c0816 the full
    /// fallback moved <c>if x:</c> to the body's level — right when the over-indent was a typo, silent-wrong
    /// when it was a property's observer block; now both get no edits.
    /// </summary>
    [Fact]
    public void UnexpectedIndent_FullFallback_GetsNoEdits()
    {
        const string overIndented = TwoSpace + "    if x:\n";
        FormattingFallback.ReindentDocument(overIndented).Should().Be("def main():\n    x: int = 1\n    print(x)\n    if x:\n");
        _driver.Full(overIndented).Edits.Should().BeEmpty();

        FormattingFallback.IndentOnlyPreserved(ClosedLiteral, WithLine(ClosedLiteral, 4, "    if s:")).Should().BeFalse();
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

    /// <summary>S3 <c>depth</c>: the fallbacks moved the observer block one level shallow @ aef9c0816.</summary>
    [Fact]
    public void S3_ObserverBlock_FullAndRangeFallbacks_GetNoEdits()
    {
        FormattingFallback.ReindentDocument(Observers)
            .Should().Be(WithLine(WithLine(Observers, 2, "    before_set(new_value):"), 3, "        print(new_value)"));
        RangeCandidate(Observers, 2).Should().Be(WithLine(Observers, 2, "    before_set(new_value):"));

        _driver.Full(Observers).Edits.Should().BeEmpty();
        _driver.Range(Observers, LineRange(Observers, 2)).Edits.Should().BeEmpty();
    }

    /// <summary>S5 <c>depth</c>: the full fallback moved <c>main</c>'s body to column 0 @ aef9c0816.</summary>
    [Fact]
    public void S5_TriplesThatRePair_FullFallback_GetsNoEdits()
    {
        FormattingFallback.ReindentDocument(DStringRepaired)
            .Should().Be(WithLine(WithLine(DStringRepaired, 3, "msg: str = d\"\"\""), 7, "print(repr(msg))"));
        _driver.Full(DStringRepaired).Edits.Should().BeEmpty();
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

    // ---- P22f (#2271): a literal lost WITHOUT a lexer abort — the error budget, a re-paired closer, a
    // dropped delimiter line. By direction: each unchecked candidate is the damaged text the handler
    // applied @ 3c70ea492 (driven over stdio, .claude/tmp/p22f-probes/out2.json and xout.json); the
    // handler now returns no edits. The direction controls below are documents that lose NO literal
    // and keep the repair they got @ 3c70ea492 byte for byte. The constants are shared with
    // OnTypeFormattingTests and IndentationServiceLiteralStateTests.

    /// <summary>The lexer's error budget (<c>Lexer.MaxErrors</c>, 25 @ 3c70ea492 — N1's bytes are those of that budget).</summary>
    internal static readonly int Budget = new Compiler.Lexer.Lexer("").MaxErrors;

    /// <summary>
    /// N1: the budget's worth of unterminated short strings above <c>main</c>; the compiler's lexer stops at its
    /// budget with <c>main</c>'s closed triple-quoted string unread (<c>UnreadRemainder</c>). The editor's indent map
    /// lexes past the budget (R-FP, #2273): the string is read and its lines are literal lines.
    /// </summary>
    internal static readonly string N1 =
        string.Concat(Enumerable.Repeat("x = \"abc\n", Budget))
        + "def main():\n    s = \"\"\"\n    key:\n      sub: 1\n    \"\"\"\n    print(s)\n";

    /// <summary>N1r: N1 with its <c>print(s)</c> at 2 spaces — past the budget, a code line to repair below a string the map reads (R-FP).</summary>
    internal static readonly string N1r = N1.Replace("    print(s)", "  print(s)", StringComparison.Ordinal);

    /// <summary>
    /// #2273's program: the budget's worth of unterminated short strings, then a method whose signature continues a
    /// bracket at column 0. @ 926670989 every line past the stop read as a dropped code line that starts a logical
    /// line; the column-0 continuation popped every block and Format Document moved <c>        ...</c> out of
    /// <c>method</c>. <see cref="P2273Under"/> is the same program one error under the budget.
    /// </summary>
    internal static readonly string P2273 =
        string.Concat(Enumerable.Repeat("x = \"abc\n", Budget)) + "class Foo:\n    def method(  # c1\nself) -> None:\n        ...\n";

    /// <summary><see cref="P2273"/> with one error line fewer: the lexer reads every line (the twin the plan measured @ 729bf1e7d).</summary>
    internal static readonly string P2273Under = P2273.Substring("x = \"abc\n".Length);

    /// <summary>N1's string line at width 6 (0-based).</summary>
    internal static readonly int N1SubLine = Budget + 3;

    /// <summary>N1t: a 2-space body past the budget, a 2-space string at its end.</summary>
    internal static readonly string N1t =
        "def main():\n" + string.Concat(Enumerable.Repeat("  x = 1\n", 30)) + "  s = \"\"\"\n      key\n  \"\"\"\n  print(s)\n";

    /// <summary>
    /// BSesc (verify round @ 5c8d81f28): the 25th error is a short string aborting mid-line before an opener,
    /// so the budget stop lands INSIDE the aborted <c>"\q"</c>; read from the <c>q</c>, the remainder paired
    /// its quotes the wrong way and saw no opener — the routes re-indented <c>sub: 1</c>. Past R-FP (#2273) the map's
    /// lexer has no budget: the 25th error is an ordinary mid-line abort whose dropped span <c>"\q" + """</c> holds the
    /// opener — <c>DroppedOpener</c>, a refusal still.
    /// </summary>
    internal static readonly string BSesc =
        string.Concat(Enumerable.Repeat("x = \"abc\n", Budget - 1))
        + "def main():\n    s = \"\\q\" + \"\"\"\n    key:\n      sub: 1\n    \"\"\" + '\"\"\"'\n    print(s)\n";

    /// <summary>BSesc's string line at width 6 (0-based).</summary>
    internal static readonly int BSescSubLine = Budget + 2;

    /// <summary>B1 (verify round @ 5c8d81f28): BSesc's twin whose triple is closed on its line — nothing is lost (direction control).</summary>
    internal static readonly string B1 =
        string.Concat(Enumerable.Repeat("x = \"abc\n", Budget - 1)) + "y = \"\\q\" + \"\"\"abc\"\"\"\n"
        + "def main():\n" + string.Concat(Enumerable.Repeat("  x = 1\n", 30));   // N1c's text (declared below: a static initializer runs in textual order)

    /// <summary>N1c: a 2-space body past the budget, no literal (direction control).</summary>
    internal static readonly string N1c = "def main():\n" + string.Concat(Enumerable.Repeat("  x = 1\n", 30));

    /// <summary>N1s: a 2-space body of short strings past the budget (direction control).</summary>
    internal static readonly string N1s = "def main():\n" + string.Concat(Enumerable.Repeat("  print(\"hi\")\n", 30));

    /// <summary>N2 (#2271's document): a stray <c>"""</c> pairs with <c>s</c>'s opener; <c>s</c>'s closer re-pairs into <c>'"""'</c>.</summary>
    internal const string N2 = "\"\"\"\ndef main():\n    s = \"\"\"\n      key: value\n    \"\"\"\n    t = '\"\"\"'\n    print(s, t)\n";

    /// <summary>N2c: N2 with the orphan quote swallowed by the comment's apostrophe — no SPY0001.</summary>
    internal const string N2c = "\"\"\"\ndef main():\n    s = \"\"\"\n      key: value\n    \"\"\"\n    t = '\"\"\"'  # don't\n    print(s, t)\n";

    /// <summary>N2w: N2's quote characters swapped — a stray <c>'''</c> above a <c>"""</c> docstring.</summary>
    internal const string N2w = "'''\ndef f():\n    \"\"\"\n    doc\n    \"\"\"\n    x = \"'''\"\n    return x\n";

    /// <summary>N4 (#2271's document): the opener and closer lines at 2 spaces are dropped whole (SPY0013).</summary>
    internal const string N4 = "def main():\n  s = \"\"\"\n        key: value\n  \"\"\"\n  print(s)\n";

    /// <summary>N4tab: N4's delimiter lines tab-indented (SPY0012).</summary>
    internal const string N4tab = "def main():\n\ts = \"\"\"\n        key: value\n\t\"\"\"\n\tprint(s)\n";

    /// <summary>N4f: a single-quoted f-string whose replacement field spans lines, on a dropped line.</summary>
    internal const string N4f = "def main():\n  x = f\"{', '.join(\n        names\n      )}\"\n  print(x)\n";

    /// <summary>X6: an unexpected character before the opener drops the rest of its line (SPY0015); the closer drops at SPY0014.</summary>
    internal const string X6 = "def main():\n    x = $\"\"\"\n        key: value\n    \"\"\"\n    print(x)\n";

    /// <summary>N4q: a dropped line holding a short string (direction control).</summary>
    internal const string N4q = "def main():\n  x = \"hi\"\n  print(x)\n";

    /// <summary>N4t: a dropped line holding a triple inside a short string (direction control).</summary>
    internal const string N4t = "def main():\n  t = '\"\"\"'\n  print(t)\n";

    /// <summary>X4: a dropped line holding a triple-quoted literal closed on its own line (direction control).</summary>
    internal const string X4 = "def main():\n  s = \"\"\"abc\"\"\"\n  print(s)\n";

    /// <summary>The names of the routes whose request returned edits (every route named, not only the first).</summary>
    private static string[] RoutesThatEdit(params (string Route, IReadOnlyList<TextEdit> Edits)[] cells) =>
        cells.Where(c => c.Edits.Count > 0).Select(c => c.Route).ToArray();

    private static LspRange Lines(int start, int end) => new(new Position(start, 0), new Position(end, 0));

    /// <summary>The range fallback's unchecked candidate for <paramref name="range"/>, the handler's selected lines, applied.</summary>
    private static string RangeCandidate(string text, LspRange range)
    {
        var (start, end) = new Compiler.Formatting.FormatSelection(range.Start.Line, range.End.Line, range.End.Character)
            .SelectedLines(Compiler.Formatting.LineDiff.Split(text).Lines);
        return LspFormattingDriver.ApplyStrict(text, SharpyRangeFormattingHandler.ComputeIndentOnlyRangeEdits(text, start, end));
    }

    [Fact]
    public void BSesc_BudgetStopInsideAnAbortedShortString_FullAndRangeFallbacks_GetNoEdits()
    {
        var damaged = WithLine(BSesc, BSescSubLine, "        sub: 1");
        FormattingFallback.ReindentDocument(BSesc).Should().Be(damaged);
        RangeCandidate(BSesc, BSescSubLine).Should().Be(damaged);

        RoutesThatEdit(
            ("full", _driver.Full(BSesc).Edits),
            ("range-line", _driver.Range(BSesc, LineRange(BSesc, BSescSubLine)).Edits)).Should().BeEmpty();
    }

    /// <summary>
    /// R-FP (#2273): past the compiler's error budget the indent map still reads the string, so its lines are
    /// literal lines — the candidate leaves <c>      sub: 1</c> byte-identical (@ 926670989 it re-indented it and the
    /// fact refused the whole document) — and a misindented code line below it is repaired (N1r; @ 926670989 no
    /// edits). The compiler's own lexer still records <c>UnreadRemainder</c> (IndentationServiceLiteralStateTests).
    /// </summary>
    [Fact]
    public void N1_PastTheBudget_TheStringIsRead_ItsLinesAreUntouched_AndCodeBelowIsRepaired()
    {
        FormattingFallback.ReindentDocument(N1).Should().Be(N1);
        RangeCandidate(N1, N1SubLine).Should().Be(N1);
        RoutesThatEdit(
            ("full", _driver.Full(N1).Edits),
            ("range-line", _driver.Range(N1, LineRange(N1, N1SubLine)).Edits)).Should().BeEmpty("N1 is already indented: nothing to repair");

        var repaired = N1r.Replace("  print(s)", "    print(s)", StringComparison.Ordinal);
        _driver.Full(N1r).Applied.Should().Be(repaired, "the string's lines are kept, the 2-space code line is repaired");
        _driver.Range(N1r, LineRange(N1r, N1SubLine + 2)).Applied.Should().Be(repaired);
    }

    /// <summary>
    /// #2273 (R-FP): past the budget the column-0 continuation of <c>method</c>'s signature is read as one, so the
    /// fallback applies exactly what it applies one error under the budget: the continuation line re-indented to
    /// 4 spaces and <c>        ...</c> kept at its depth (@ 926670989 full and range-whole moved it to 4 spaces).
    /// </summary>
    [Fact]
    public void P22g_BracketContinuationPastTheBudget_FullAndRangeWhole_KeepTheBody()
    {
        var keptUnder = P2273Under.Replace("\nself) -> None:", "\n    self) -> None:", StringComparison.Ordinal);
        var kept = P2273.Replace("\nself) -> None:", "\n    self) -> None:", StringComparison.Ordinal);
        _driver.Full(P2273Under).Applied.Should().Be(keptUnder, "the control one error under the budget");
        _driver.Full(P2273).Applied.Should().Be(kept);
        _driver.Range(P2273, Lines(0, Budget + 4)).Applied.Should().Be(kept);
    }

    /// <summary>
    /// N1t: a 2-space body of 30 lines (each a SPY0013 that error recovery drops) and a 2-space string at its end.
    /// Past the budget the map now reads every line (R-FP), and the string's opener line <c>  s = """</c> is itself
    /// a 2-space line recovery drops whole: <c>DroppedOpener</c> — a refusal still, by the dropped line, no longer by
    /// the budget.
    /// </summary>
    [Fact]
    public void N1t_BudgetStopAbove2SpaceString_RangeFallback_GetsNoEdits()
    {
        RangeCandidate(N1t, 32).Should().Be(WithLine(N1t, 32, "    key"));
        _driver.Range(N1t, LineRange(N1t, 32)).Edits.Should().BeEmpty();
    }

    [Theory]
    [InlineData(N2)]
    [InlineData(N2c)]
    public void N2_ClosedStringRePairedIntoAShortString_FullAndRangeFallbacks_GetNoEdits(string document)
    {
        FormattingFallback.ReindentDocument(document)
            .Should().Be(WithLine(WithLine(WithLine(document, 3, "key: value"), 4, "\"\"\""), 6, "print(s, t)"));
        RangeCandidate(document, 3).Should().Be(WithLine(document, 3, "key: value"));

        RoutesThatEdit(
            ("full", _driver.Full(document).Edits),
            ("range-line", _driver.Range(document, LineRange(document, 3)).Edits)).Should().BeEmpty();
    }

    [Fact]
    public void N2w_SwappedQuoteCharacters_FullFallback_GetsNoEdits()
    {
        FormattingFallback.ReindentDocument(N2w).Should().Be(WithLine(N2w, 6, "return x"));
        _driver.Full(N2w).Edits.Should().BeEmpty();
    }

    [Fact]
    public void N4_DroppedDelimiterLines_RangeFallback_GetsNoEdits()
    {
        // Full and range-whole were refused @ 3c70ea492 already (clause 3); the selections inside were not.
        var damaged = WithLine(N4, 2, "    key: value");
        RangeCandidate(N4, 2).Should().Be(damaged);
        RangeCandidate(N4, Lines(2, 3)).Should().Be(damaged);
        RangeCandidate(N4, Lines(2, 4)).Should().Be(WithLine(damaged, 3, "    \"\"\""));

        RoutesThatEdit(
            ("range-line", _driver.Range(N4, LineRange(N4, 2)).Edits),
            ("range (2,0)-(3,0)", _driver.Range(N4, Lines(2, 3)).Edits),
            ("range (2,0)-(4,0)", _driver.Range(N4, Lines(2, 4)).Edits)).Should().BeEmpty();
    }

    [Fact]
    public void N4f_DroppedLineOpeningAMultiLineHole_RangeFallback_GetsNoEdits()
    {
        RangeCandidate(N4f, 2).Should().Be(WithLine(N4f, 2, "    names"));
        _driver.Range(N4f, LineRange(N4f, 2)).Edits.Should().BeEmpty();
    }

    [Fact]
    public void X6_OpenerDroppedAtAnUnexpectedCharacter_FullAndRangeFallbacks_GetNoEdits()
    {
        var damaged = WithLine(X6, 2, "    key: value");
        FormattingFallback.ReindentDocument(X6).Should().Be(damaged);
        RangeCandidate(X6, Lines(2, 3)).Should().Be(damaged);

        RoutesThatEdit(
            ("full", _driver.Full(X6).Edits),
            ("range-line", _driver.Range(X6, Lines(2, 3)).Edits)).Should().BeEmpty();
    }

    /// <summary>
    /// C1 (final verification @ 4b7428567): a short string aborting mid-line (SPY0004, an invalid escape)
    /// before the opener drops the rest of its line with the lexer INSIDE that string; the same on the
    /// closer's line keeps the triple count even, so no read aborts. Module level.
    /// </summary>
    internal const string C1 = "x = \"\\d+\" + \"\"\"\n    key: value\n\"\\d+\" + \"\"\"\nprint(x)\n";

    /// <summary>C1's block-level twin: the content and the closer at width 8.</summary>
    internal const string C1Block = "def main():\n    x = \"\\q\" + \"\"\"\n        key: value\n        \"\\q\" + \"\"\"\n    print(x)\n";

    [Fact]
    public void C1_OpenerDroppedByAShortStringAbortingMidLine_FullAndRangeFallbacks_GetNoEdits()
    {
        var damaged = WithLine(C1, 1, "key: value");
        FormattingFallback.ReindentDocument(C1).Should().Be(damaged);
        RangeCandidate(C1, 1).Should().Be(damaged);
        RangeCandidate(C1Block, 2).Should().Be(WithLine(C1Block, 2, "    key: value"));

        RoutesThatEdit(
            ("full", _driver.Full(C1).Edits),
            ("range-whole", _driver.Range(C1, Lines(0, 4)).Edits),
            ("range-line", _driver.Range(C1, LineRange(C1, 1)).Edits),
            ("block range-line", _driver.Range(C1Block, LineRange(C1Block, 2)).Edits)).Should().BeEmpty();
    }

    /// <summary>
    /// XBT (wave-5 sibling @ 390d701d7): C1's shape with the abort an unexpected character before a
    /// backtick-delimited name holding an apostrophe. Read flat, the apostrophe opened a short string that
    /// swallowed the opener, and Format Document rewrote <c>    key: value</c>.
    /// </summary>
    internal const string Xbt = "x = $`it's` + \"\"\"\n    key: value\n$`it's` + \"\"\"\nprint(x)\n";

    [Fact]
    public void Xbt_OpenerDroppedAfterABacktickName_FullAndRangeFallbacks_GetNoEdits()
    {
        var damaged = WithLine(Xbt, 1, "key: value");
        FormattingFallback.ReindentDocument(Xbt).Should().Be(damaged);
        RangeCandidate(Xbt, 1).Should().Be(damaged);

        RoutesThatEdit(
            ("full", _driver.Full(Xbt).Edits),
            ("range-whole", _driver.Range(Xbt, Lines(0, 4)).Edits),
            ("range-line", _driver.Range(Xbt, LineRange(Xbt, 1)).Edits)).Should().BeEmpty();
    }

    /// <summary>
    /// E1par2 (second verification @ 37e5c1d02): C1Block's shape with the aborting string an f-string whose
    /// replacement field holds a string of the f-string's own quote character. Read the way the lexer reads
    /// a hole, the dropped span holds the opener; read flat (@ 37e5c1d02), the nested <c>"</c> closed the
    /// f-string, the opener paired away, and the range fallback rewrote <c>        key: value</c>. Only the
    /// range-line route is a cell of the fact: Format Document and the whole-document selection are refused
    /// for another clause with the hooks reverted too (verify round @ 5c8d81f28), so they discriminate nothing.
    /// </summary>
    internal const string E1par2 = "def main():\n    x = f\"{'\"'}\\q\" + \"\"\"\n        key: value\n        f\"{'\"'}\\q\" + \"\"\"\n    print(x)\n";

    [Fact]
    public void E1par2_OpenerDroppedAfterAStringNestedInAReplacementField_RangeFallback_GetsNoEdits()
    {
        RangeCandidate(E1par2, 2).Should().Be(WithLine(E1par2, 2, "    key: value"));

        RoutesThatEdit(
            ("range-line", _driver.Range(E1par2, LineRange(E1par2, 2)).Edits)).Should().BeEmpty();
    }

    /// <summary>
    /// W12 (second verification @ 37e5c1d02): the dropped 2-space line <c>y = f"{'{'}</c> closes its
    /// replacement field — the <c>{</c> is inside a nested string — so it ends at its line and the line
    /// routes keep the 4-space repair they applied @ 3c70ea492 (refused @ 37e5c1d02).
    /// </summary>
    [Fact]
    public void DirectionControl_BraceInAStringNestedInAClosedField_LineRoutesKeepTheirRepair()
    {
        const string document = "def main():\n  x: int = 1\n  y = f\"{'{'}\n  print(x)\n";

        _driver.Range(document, LineRange(document, 1)).Applied.Should().Be(WithLine(document, 1, "    x: int = 1"));
        _driver.OnType(document, 1).Applied.Should().Be(WithLine(document, 1, "    x: int = 1"));
        _driver.Range(document, LineRange(document, 3)).Applied.Should().Be(WithLine(document, 3, "    print(x)"));
    }

    /// <summary>The owner's 2-space document (<see cref="TwoSpace"/>) repaired to 4 spaces.</summary>
    private const string TwoSpaceRepaired = "def main():\n    x: int = 1\n    print(x)\n";

    public static TheoryData<string, string, string> NoLiteralLost => new()
    {
        { "N1c", N1c, "def main():\n" + string.Concat(Enumerable.Repeat("    x = 1\n", 30)) },
        // verify round @ 5c8d81f28: the budget stop inside an aborted "\q" before a triple closed on its line — read
        // from the `q` the remainder saw an opener (UnreadRemainder @ 5c8d81f28); repaired @ 3c70ea492
        { "B1", B1, string.Concat(Enumerable.Repeat("x = \"abc\n", Budget - 1)) + "y = \"\\q\" + \"\"\"abc\"\"\"\n" + "def main():\n" + string.Concat(Enumerable.Repeat("    x = 1\n", 30)) },
        { "N1s", N1s, "def main():\n" + string.Concat(Enumerable.Repeat("    print(\"hi\")\n", 30)) },
        { "N4q", N4q, "def main():\n    x = \"hi\"\n    print(x)\n" },
        { "N4t", N4t, "def main():\n    t = '\"\"\"'\n    print(t)\n" },
        { "X4", X4, "def main():\n    s = \"\"\"abc\"\"\"\n    print(s)\n" },
        // final verification @ 4b7428567: repaired @ 3c70ea492 (the verifier's base outputs), refused @ 4b7428567
        { "Dbud_f", N1c + "  y = f\"x\n", "def main():\n" + string.Concat(Enumerable.Repeat("    x = 1\n", 30)) + "    y = f\"x\n" },
        { "Da1", "z = \"\"\"a\"\"\"\"\n" + TwoSpace, "z = \"\"\"a\"\"\"\"\n" + TwoSpaceRepaired },
        { "Da2", "z = \"\"\"a\"\"\"'b\n" + TwoSpace, "z = \"\"\"a\"\"\"'b\n" + TwoSpaceRepaired },
        { "Da3", "z = \"\"\"\"\"\"\" \n" + TwoSpace, "z = \"\"\"\"\"\"\" \n" + TwoSpaceRepaired },
        // the dropped span read from INSIDE the aborted "\q" paired its quotes the wrong way and saw an opener
        // in the closed """abc""" (DroppedOpener @ 4b7428567); repaired @ 3c70ea492 (the p22e-lead proxy)
        { "Dq_closed", "x = \"\\q\" + \"\"\"abc\"\"\"\n" + TwoSpace, "x = \"\\q\" + \"\"\"abc\"\"\"\n" + TwoSpaceRepaired },
        // second verification @ 37e5c1d02: a string nested in a replacement field was read flat — a closed
        // nested triple as an opener (M16), a '{' in a nested string as an open field (M17); repaired @ 3c70ea492
        { "M16", "y = f\"{\"\"\"a\"\"\"}\\q\" + 'z'\n" + TwoSpace, "y = f\"{\"\"\"a\"\"\"}\\q\" + 'z'\n" + TwoSpaceRepaired },
        { "M17", "y = f\"{'{'}\\q\n" + TwoSpace, "y = f\"{'{'}\\q\n" + TwoSpaceRepaired },
    };

    /// <summary>
    /// Direction controls: a lex that stops at its budget, drops a line holding a string that does not
    /// span lines, or closes a triple run on the line it opened on lost no literal — the full fallback keeps
    /// the 4-space repair it applied @ 3c70ea492. <c>Dbud_f</c>: the unread remainder's unterminated
    /// <c>f"x</c> has no open replacement field. <c>Da1</c>–<c>Da3</c>: a single-line <c>"""a"""</c> run
    /// followed by a quote (arm (a) of the re-pair fact, restricted to runs that spanned lines).
    /// <c>Dq_closed</c>: a short string aborting mid-line before a triple closed on its line.
    /// </summary>
    [Theory]
    [MemberData(nameof(NoLiteralLost))]
    public void DirectionControl_NoLiteralLost_FullFallbackKeepsItsRepair(string name, string document, string repaired)
    {
        _driver.Full(document).Applied.Should().Be(repaired, "{0} lost no literal; @ 3c70ea492 the full fallback repaired it to this text", name);
    }

    /// <summary>
    /// Dfprint (final verification @ 4b7428567): the dropped 2-space line <c>print(f"total: {x}</c> is an
    /// unterminated f-string whose replacement field is closed — it loses no literal, as its plain-string twin
    /// does — so the line routes keep the 4-space repair they applied @ 3c70ea492 (refused @ 4b7428567) on line 1.
    /// By direction (P22h, R-FU, #2279): the dropped line also leaves its call's <c>(</c> open at its END — a bracket
    /// the lexer never sees closed (<c>Lexer.BracketLeftOpenAtLine</c> = 3) — so <c>  print(x)</c> below it is the
    /// bracket's to the lexer and no route edits it: range-line 3 repaired it @ ac3235c2a and applies nothing now
    /// (repair → no edit, by the ruling's words; the @ ac3235c2a text is refused by clause 7).
    /// </summary>
    [Fact]
    public void DirectionControl_UnterminatedFStringWithNoOpenField_LineRoutesKeepTheirRepairAboveTheOpenBracket()
    {
        const string document = "def main():\n  x: int = 1\n  print(f\"total: {x}\n  print(x)\n";

        _driver.Range(document, LineRange(document, 1)).Applied.Should().Be(WithLine(document, 1, "    x: int = 1"));
        _driver.OnType(document, 1).Applied.Should().Be(WithLine(document, 1, "    x: int = 1"));

        IndentationService.BuildIndentMap(document).OpenBracketLine.Should().Be(3);
        FormattingFallback.IndentOnlyPreserved(document, WithLine(document, 3, "    print(x)")).Should().BeFalse("the @ ac3235c2a range-line 3 text edits a line below the open bracket");
        _driver.Range(document, LineRange(document, 3)).Edits.Should().BeEmpty("direction: repaired @ ac3235c2a, below the bracket now (R-FU)");
    }

    // ---- P22g (found by the route-parity sweep's S6x direction control): error recovery emits no Newline, so the
    // line after an error read as a continuation of the dropped one. The lexer now records where recovery resumed
    // (Lexer.RecoveryResumes); LiteralSpans resets there, and (P22h, R-FU) the indent map starts a logical line there
    // unless the dropped line continues — the indent-only check judges that line like any statement.

    /// <summary>
    /// An f-string abandoned while its format spec is typed (<c>a = f"{x:</c>), right above a triple f-string whose
    /// replacement field spans lines, and a 2-space line to repair at the end. @ 926670989 LiteralSpans paired the
    /// abandoned start with the triple's end, so the field's line <c>        x * 2</c> read as code: full and
    /// range-whole rewrote it to 4 spaces, and range-line 3 too.
    /// </summary>
    internal const string AbandonedFStringAboveAField =
        "def main():\n    a = f\"{x:\n    print(f\"\"\"total: {\n        x * 2\n    }\"\"\")\n    b = 1\n  c = 2\n";

    [Fact]
    public void AnFStringAbandonedByRecovery_TheNextLiteralsFieldLine_IsUntouched_AndTheCodeIsRepaired()
    {
        const string d = AbandonedFStringAboveAField;
        var repaired = WithLine(d, 6, "    c = 2");
        _driver.Full(d).Applied.Should().Be(repaired, "the field line is string content; c = 2 is the repair (@ 926670989 x * 2 moved to 4 spaces)");
        _driver.Range(d, Lines(0, 7)).Applied.Should().Be(repaired);
        _driver.Range(d, LineRange(d, 3)).Edits.Should().BeEmpty("line 3 starts inside the triple f-string's replacement field");
        _driver.OnType(d, 6).Applied.Should().Be(repaired, "direction: the code line after the literal keeps its on-type repair");
    }

    /// <summary>
    /// The verify round's lost repair (P22g, found by the refuting verifier @ 9202e3b83): when the line error recovery
    /// drops CONTINUES — a bracket left open, a trailing backslash — the next line is the user's continuation line, not
    /// a statement, and judging its depth refused the whole repair. The lexer records the continuation
    /// (RecoveryResumesAfterAContinuedLine) and the map keeps such a line a continuation.
    /// <para>
    /// By direction (P22h, R-FU, #2279): @ 926670989 and @ ac3235c2a full and range-whole ALSO re-indented the line
    /// below a bracket left open (<c>        a)</c> → <c>    a)</c>). A bracket open at the dropped line's END is one the
    /// lexer never sees closed (<c>Lexer.BracketLeftOpenAtLine</c>), and no route edits a line below its opener: line 5
    /// is untouched now while <c>  return 1</c> above keeps its repair (repair → no edit, the ruling's own words; the
    /// @ ac3235c2a text is refused by clause 7). The backslash row leaves no bracket open: its continuation line is still
    /// aligned to its block (<c>aligned</c>), and the line after it is not a logical start.
    /// </para>
    /// </summary>
    public static TheoryData<string, string, bool> ContinuedLineDroppedByRecovery => new()
    {
        { "call $", "    x = foo($,\n        a)\n    print(x)\n", false },
        { "call string", "    x = foo(\"abc,\n        a)\n    print(x)\n", false },
        { "list $", "    xs = [1, 2, $\n        3]\n    print(xs)\n", false },
        { "backslash", "    y = 1 + $ \\\n        2\n    print(y)\n", true },
        { "bracket opened after the abort", "    x = $ foo(1,\n        2)\n    print(x)\n", false },
        { "bracket opened after a closed short string that aborted", "    x = foo(\"\\q\", [1,\n        2])\n    print(x)\n", false },
    };

    [Theory]
    [MemberData(nameof(ContinuedLineDroppedByRecovery))]
    public void DirectionChange_AContinuedLineDroppedByRecovery_TheLineAboveKeepsItsRepair_OnlyABackslashContinuationIsAligned(string name, string body, bool aligned)
    {
        var document = "def f():\n  return 1\n\ndef main():\n" + body;
        var lines = document.Split('\n');
        var alignedText = WithLine(WithLine(document, 1, "    return 1"), 5, "    " + lines[5].TrimStart());
        var repaired = aligned ? alignedText : WithLine(document, 1, "    return 1");
        _driver.Full(document).Applied.Should().Be(repaired, name);
        _driver.Range(document, Lines(0, lines.Length - 1)).Applied.Should().Be(repaired, name);

        var map = IndentationService.BuildIndentMap(document);
        map.LogicalLineStarts.Should().NotContain(6, "{0}: the line after a dropped line that continues is its continuation", name);
        if (!aligned)
        {
            map.OpenBracketLine.Should().Be(5, name);
            FormattingFallback.IndentOnlyPreserved(document, alignedText).Should().BeFalse("{0}: the @ ac3235c2a text edits a line below the open bracket", name);
        }
    }

    /// <summary>
    /// The sibling of <see cref="ContinuedLineDroppedByRecovery"/> (P22g verify round, the prober's and reviewer's
    /// cells): a dropped line whose bracket the given-up text CLOSES — a header <c>if foo($):</c>, a signature
    /// <c>def method(self, $) -> None:</c>, a call that closes on its line — does not continue, and neither does one
    /// ending in a backslash inside a comment: the next line is the user's body or next statement, hidden from the
    /// logical lines (no Newline follows a recovery) and depth-judged — since P22h (R-FU) a logical start of its own. The
    /// unchecked candidate moves it a block out (@ 926670989 and @ c08cf1f03 every route applied that: the body left its
    /// <c>if</c>, <c>...</c> left <c>method</c>); the routes refuse. Positive control:
    /// <see cref="DirectionChange_AContinuedLineDroppedByRecovery_TheLineAboveKeepsItsRepair_OnlyABackslashContinuationIsAligned"/>
    /// keeps the repair above when the bracket IS open at the line's end.
    /// </summary>
    public static TheoryData<string, string, int> BracketClosedOnTheDroppedLine => new()
    {
        { "if call", "def main():\n    if foo($):\n        y = 1\n    z = 2\n  w = 3\n", 2 },
        { "for range", "def main():\n    for i in range($):\n        y = 1\n    z = 2\n  w = 3\n", 2 },
        { "with open", "def main():\n    with open($) as h:\n        y = 1\n    z = 2\n  w = 3\n", 2 },
        { "list in if", "def main():\n    if x in [1, $]:\n        y = 1\n    z = 2\n  w = 3\n", 2 },
        { "signature", "class Foo:\n    def method(self, $) -> None:\n        ...\n  x = 1\n", 2 },
    };

    [Theory]
    [MemberData(nameof(BracketClosedOnTheDroppedLine))]
    public void ABracketClosedOnTheDroppedLine_TheNextLineKeepsItsBlock_EveryRouteRefuses(string name, string document, int bodyLine)
    {
        var lines = document.Split('\n');
        FormattingFallback.ReindentDocument(document).Split('\n')[bodyLine].Should().NotBe(lines[bodyLine],
            $"{name}: the unchecked candidate moves the line after the dropped one out of its block");
        RoutesThatEdit(
            ("full", _driver.Full(document).Edits),
            ("range-whole", _driver.Range(document, Lines(0, lines.Length - 1)).Edits),
            ($"range-line {bodyLine - 1}", _driver.Range(document, LineRange(document, bodyLine - 1)).Edits),
            ($"ontype@{bodyLine - 1}", _driver.OnType(document, bodyLine - 1).Edits)).Should().BeEmpty(name);
    }

    /// <summary>
    /// The 8-space twins (the reviewer's cells): the line after the dropped one is depth-judged, so re-indenting the
    /// statement's first line ALONE (on-type, range-line on <c>x = foo(</c>, <c>d = {</c>) would leave the line after
    /// the dropped one a block deeper and is refused (re-indenting a continuation line alone is an alignment, not
    /// a block move, and stays allowed); the full and range-whole fallbacks re-indent every line to 4 spaces and keep
    /// their repair (direction).
    /// </summary>
    public static TheoryData<string, string, int> WideBracketClosedOnTheDroppedLine => new()
    {
        { "wide call closed", "def main():\n        x = foo($)\n        print(x)\n", 1 },
        { "wide call closed on its continuation", "def main():\n        x = foo(\n            1, $)\n        print(x)\n", 1 },
        { "wide dict closed", "def main():\n        d = {\n            'a': $}\n        print(d)\n", 1 },
        { "backslash in a comment", "def main():\n        x = $  # path C:\\\n        print(x)\n", 1 },
    };

    [Theory]
    [MemberData(nameof(WideBracketClosedOnTheDroppedLine))]
    public void AWideBracketClosedOnTheDroppedLine_PartialReindentsAreRefused_FullKeepsItsRepair(string name, string document, int firstLine)
    {
        var lines = document.Split('\n');
        RoutesThatEdit(
            ($"range-line {firstLine}", _driver.Range(document, LineRange(document, firstLine)).Edits),
            ($"ontype@{firstLine}", _driver.OnType(document, firstLine).Edits)).Should().BeEmpty(
            $"{name}: re-indenting the statement's first line alone moves the line after the dropped one a block deeper (@ 926670989 and @ c08cf1f03 it did)");
        var repaired = string.Join('\n', lines.Select((l, i) => i == 0 || l.Length == 0 ? l : "    " + l.TrimStart()));
        _driver.Full(document).Applied.Should().Be(repaired, $"{name}: direction — the full fallback re-indents every line and keeps its repair");
        _driver.Range(document, Lines(0, lines.Length - 1)).Applied.Should().Be(repaired, name);
    }

    /// <summary>
    /// The budget twin of <see cref="BracketClosedOnTheDroppedLine"/>'s signature row (the prober's R9b): past 24
    /// error lines, #2273's program with the abort on the continuation line. @ 926670989 the budget refused it;
    /// after R-FP the indent map reads it, and the first continued-line spelling moved <c>...</c> to class level.
    /// </summary>
    [Fact]
    public void ABracketClosedOnTheDroppedLine_PastTheBudget_FullAndRangeWholeRefuse()
    {
        var document = string.Concat(Enumerable.Repeat("x = \"abc\n", Budget - 1))
            + "class Foo:\n    def method(  # c1\nself, $) -> None:\n        ...\n";
        var lines = document.Split('\n');
        FormattingFallback.ReindentDocument(document).Split('\n')[Budget + 2].Should().NotBe(lines[Budget + 2],
            "the unchecked candidate moves ... out of method");
        RoutesThatEdit(
            ("full", _driver.Full(document).Edits),
            ("range-whole", _driver.Range(document, Lines(0, lines.Length - 1)).Edits)).Should().BeEmpty();
    }

    /// <summary>
    /// An 8-space document whose <c>dr"""</c> closer line aborts (<c>""" + $</c>): the line after it follows a
    /// recovery. @ 926670989 on-type and range-line 1 re-indented line 1 alone to 4 spaces and left
    /// <c>print(s)</c> at 8 — one block deeper than its sibling. Full re-indents both and keeps its repair.
    /// P22g judged the line as one "hidden after recovery"; since P22h (R-FU) it starts a logical line of its own and
    /// clause 6 judges it as any statement (re-pointed, not re-spelled: every route's outcome is unchanged).
    /// </summary>
    internal const string WideCloserLineAbort =
        "def main():\n        s: str = dr\"\"\"\n        \\d+\n        \\s+\n        \"\"\" + $\n        print(s)\n";

    [Fact]
    public void ALineAfterRecovery_StartsALogicalLine_KeepsItsBlock_PartialReindentsAreRefused()
    {
        const string d = WideCloserLineAbort;
        IndentationService.BuildIndentMap(d).LogicalLineStarts.Should().Contain(6, "print(s) follows a recovery that does not continue");
        RoutesThatEdit(
            ("ontype@1", _driver.OnType(d, 1).Edits),
            ("range-line 1", _driver.Range(d, LineRange(d, 1)).Edits)).Should().BeEmpty("re-indenting line 1 alone moves line 5 into a deeper block");
        _driver.Full(d).Applied.Should().Be(WithLine(WithLine(d, 1, "    s: str = dr\"\"\""), 5, "    print(s)"),
            "direction: the full fallback re-indents both lines and keeps its repair");
    }

    // ---- P22g Phase 3 (#2275, R-FR): an abort on the close line of a re-paired run, whatever its code.

    /// <summary>N2's shape (a stray triple, a closed block, the orphan line 5): its close line is line 5, its string line 3.</summary>
    private static string CloseLineN2(string orphanLine) =>
        "\"\"\"\ndef main():\n    s = \"\"\"\n      key: value\n    \"\"\"\n    " + orphanLine + "\n    print(s)\n";

    /// <summary>
    /// #2275's documents (its cell names): the orphan line aborts after the re-paired closer at SPY0015, SPY0018,
    /// SPY0007, SPY0008 or SPY0022, and the orphan's quote lies in the text the lexer gives up. @ 926670989 full,
    /// range-line 3 and on-type 3 each rewrote <c>      key: value</c> (string content); IB-bt is the stray inside the
    /// block. R-btname is not here: <see cref="KnownLimit_RBtname_StillEdits_SeeIssue2274"/>.
    /// </summary>
    public static TheoryData<string, string> CloseLineAbortDocuments => new()
    {
        { "R3-dollar", CloseLineN2("t = '\"\"\"$'") },
        { "R1", CloseLineN2("t = '\"\"\" costs $5'") },
        { "R3-dollar-sp", CloseLineN2("t = '\"\"\" $ '") },
        { "R-bt", CloseLineN2("t = '\"\"\"`'") },
        { "R2", CloseLineN2("t = '\"\"\" see `x'") },
        { "R2-bt-mid", CloseLineN2("t = '\"\"\"`x'") },
        { "R3-num__", CloseLineN2("t = '\"\"\"1__2'") },
        { "R3-hex", CloseLineN2("t = '\"\"\"0x'") },
        { "R4-fspec", CloseLineN2("t = '\"\"\"f' + \"{x:\" + str(y)") },
        { "IB-bt", "def main():\n    \"\"\"\n    s = \"\"\"\n      key: value\n    \"\"\"\n    t = '\"\"\"`'\n    print(s)\n" },
    };

    [Theory]
    [MemberData(nameof(CloseLineAbortDocuments))]
    public void R_CloseLineAbort_FullRangeLineAndOnType_GetNoEdits(string name, string document)
    {
        // The unchecked candidate rewrites the string line: the refusal is what keeps it.
        FormattingFallback.ReindentDocument(document).Split('\n')[3].Should().NotBe(document.Split('\n')[3], name);

        RoutesThatEdit(
            ("full", _driver.Full(document).Edits),
            ("range-line 3", _driver.Range(document, LineRange(document, 3)).Edits),
            ("ontype@3", _driver.OnType(document, 3).Edits)).Should().BeEmpty(name);
    }

    /// <summary>
    /// R-btname (#2274's known limit, not #2275's cure): <c>t = '"""x' + `it's`</c> — the orphan quote is READ as the
    /// opener of <c>' + `it'</c>, and the aborting backtick's text holds no quote; the lexer signature is that of a
    /// close line that lost nothing. Pinned: the routes still rewrite the string line (a cure would turn this red —
    /// record it on #2274).
    /// </summary>
    [Fact]
    public void KnownLimit_RBtname_StillEdits_SeeIssue2274()
    {
        var document = CloseLineN2("t = '\"\"\"x' + `it's`");
        _driver.Full(document).Applied.Split('\n')[3].Should().Be("key: value");
        _driver.Range(document, LineRange(document, 3)).Edits.Should().NotBeEmpty();
        _driver.OnType(document, 3).Edits.Should().NotBeEmpty();
    }

    /// <summary>
    /// M3 (#2274, a known limit of any lexer fact): a backtick name swallows the opener (<c>x = `abc + """</c>) and the
    /// lexer loses nothing it can see. Plan P22g (plan-d923d3) recorded "M3 stays" (the routes rewrite
    /// <c>key: value</c>) — but the line after the erroring one is depth-judged (e16e323eb; since P22h, R-FU, a logical
    /// start of its own), so full, range-line and on-type refuse it: an incidental gain, pinned here so that a later
    /// change that reopens the rewrite is a visible shrink of the known limit. The unchecked candidate is the positive control.
    /// </summary>
    [Fact]
    public void KnownLimit2274_M3_BacktickSwallowsTheOpener_RefusedByTheRecoveryLineJudgment()
    {
        const string document = "def main():\n    x = `abc + \"\"\"\n        key: value\n    \"\"\"  # \"\"\"\n    print(x)\n";
        FormattingFallback.ReindentDocument(document).Split('\n')[2].Should().Be("    key: value", "the unchecked candidate rewrites the string line");
        RoutesThatEdit(
            ("full", _driver.Full(document).Edits),
            ("range-line 2", _driver.Range(document, LineRange(document, 2)).Edits),
            ("ontype@2", _driver.OnType(document, 2).Edits)).Should().BeEmpty();
    }

    /// <summary>
    /// #2275's direction control (R-FR): no stray; the closer line aborts at each close-line code and drops no
    /// quote, so nothing is lost and on-type keeps its repair of a 6-space line — one clean line below the close
    /// line, since the line right after an error is read as a continuation (plan-d923d3 Current State).
    /// </summary>
    [Theory]
    [InlineData("$")]
    [InlineData("`x")]
    [InlineData("1__2")]
    [InlineData("0x")]
    [InlineData("f\"{x:")]
    public void DirectionControl_CloseLineAbortThenACleanLine_OnTypeKeepsItsRepair(string tail)
    {
        var document = "def main():\n    s = \"\"\"\n      a\n    \"\"\" + " + tail + "\n    q = 1\n      print(s)\n";
        _driver.OnType(document, 5).Applied.Should().Be(WithLine(document, 5, "    print(s)"));
    }

    // ---- P22g Phase 4 (#2276): the literal's own quote inside a format spec.

    /// <summary>SQ2 (#2276): <c>x = f"{x:">}" + """</c> in a block; the lexer aborts at the spec's quote and drops the opener.</summary>
    internal const string SQ2 = "def main():\n    x = f\"{x:\">}\" + \"\"\"\n        key: value\n        f\"{x:\">}\" + \"\"\"\n    print(x)\n";

    /// <summary>SQ3 (#2276): SQ2 at module level, its closer line at column 0.</summary>
    internal const string SQ3 = "x = f\"{x:\">}\" + \"\"\"\n    key: value\nf\"{x:\">}\" + \"\"\"\nprint(x)\n";

    /// <summary>
    /// SQ2 / SQ3: @ 926670989 range-line and full rewrote the string line <c>key: value</c> (the scanner closed the
    /// f-string at the spec's quote and never saw the opener). The routes apply nothing; the unchecked candidate
    /// shows what they would have done. NOTE (verify round): this cell does NOT discriminate #2276's arm — the string
    /// line is the line right after the erroring one, depth-judged (e16e323eb; a logical start since P22h, R-FU), so
    /// the routes refuse it with the arm reverted too. The cell that reads the <c>DroppedOpener</c> fact is
    /// <see cref="SQ_SpecQuoteAbortBeforeAnOpener_TheStringLinePastTheHiddenLine_GetsNoEdits"/> (SQ2b/SQ3b).
    /// </summary>
    [Fact]
    public void SQ_SpecQuoteAbortBeforeAnOpener_FullAndRangeLine_GetNoEdits()
    {
        FormattingFallback.ReindentDocument(SQ2).Split('\n')[2].Should().NotBe(SQ2.Split('\n')[2]);
        FormattingFallback.ReindentDocument(SQ3).Split('\n')[1].Should().NotBe(SQ3.Split('\n')[1]);
        RoutesThatEdit(
            ("SQ2 full", _driver.Full(SQ2).Edits),
            ("SQ2 range-line 2", _driver.Range(SQ2, LineRange(SQ2, 2)).Edits),
            ("SQ3 full", _driver.Full(SQ3).Edits),
            ("SQ3 range-line 1", _driver.Range(SQ3, LineRange(SQ3, 1)).Edits)).Should().BeEmpty();
    }

    /// <summary>
    /// The cells that discriminate #2276's arm at the routes: SQ2/SQ3's string line is the line right after the
    /// erroring line, which the indent-only check judges for depth since e16e323eb, so they are refused with or without
    /// the arm. Here the first string line after the abort keeps its depth and the next one is the damage —
    /// @ 926670989 (and with the arm removed) SQ2b full and range-line 3 rewrote <c>      key: value</c>, SQ3b full and
    /// range-line 2 rewrote <c>  key: value</c>; with the arm the lexer records <c>DroppedOpener</c> and nothing applies.
    /// </summary>
    [Fact]
    public void SQ_SpecQuoteAbortBeforeAnOpener_TheStringLinePastTheHiddenLine_GetsNoEdits()
    {
        const string sq2b = "def main():\n    x = f\"{x:\">}\" + \"\"\"\n    first\n      key: value\n    f\"{x:\">}\" + \"\"\"\n    print(x)\n";
        const string sq3b = "x = f\"{x:\">}\" + \"\"\"\nfirst\n  key: value\nf\"{x:\">}\" + \"\"\"\nprint(x)\n";
        RoutesThatEdit(
            ("SQ2b full", _driver.Full(sq2b).Edits),
            ("SQ2b range-line 3", _driver.Range(sq2b, LineRange(sq2b, 3)).Edits),
            ("SQ3b full", _driver.Full(sq3b).Edits),
            ("SQ3b range-line 2", _driver.Range(sq3b, LineRange(sq3b, 2)).Edits)).Should().BeEmpty();
    }

    /// <summary>
    /// The spec-quote abort's direction control: a closed spec whose quote opens no literal loses nothing, so the
    /// fallback keeps repairing a 2-space line below it (one clean line after the erroring line).
    /// </summary>
    [Fact]
    public void DirectionControl_SpecQuoteAbortWithNoOpener_FullKeepsItsRepair()
    {
        const string document = "def main():\n    x = f\"{x:\">}\"\n    q = 1\n  y = 1\n";
        _driver.Full(document).Applied.Should().Be(WithLine(document, 3, "    y = 1"));
    }

    // ---- P22h (#2279, R-FU): a bracket the lexer never saw closed freezes every line below its opener; the line after
    // an error recovery starts a logical line and is repaired. Measured over stdio @ ac3235c2a (the LSP @ base 4562b1d01
    // = be1c16fcf's; .claude/tmp/p22h-impl/p2b/out_a.txt and the plan's Current State) and asserted by direction: every
    // "no edits" cell names the text the route would apply without its refusal — the @ ac3235c2a text where that route
    // applied one — and asserts that the check refuses it.

    /// <summary>A1: a real bracket open to the end of the source above a block.</summary>
    internal const string A1 = "def main():\n    xs = [1,\n    y = 2\n    if y:\n        print(y)\n";

    /// <summary>A1b: A1 with a misindented line ABOVE the opener — it keeps its repair.</summary>
    internal const string A1b = "def main():\n      x = 1\n    xs = [1,\n    y = 2\n    if y:\n        print(y)\n";

    /// <summary>A2: <c>decorators/bracket_attr_missing_close.spy</c> — <c>@[foo</c> open to the end of the source.</summary>
    internal const string A2 = "@[foo\nclass Bar:\n    pass\n\ndef main():\n    pass\n";

    /// <summary>A3: a call "closed" inside an unterminated short string — open to the lexer at the line's end.</summary>
    internal const string A3 = "def main():\n    if foo(\"abc):\n        y = 1\n    z = 2\n  w = 3\n";

    /// <summary>A4: A3 with the <c>)</c> inside an unterminated backtick name.</summary>
    internal const string A4 = "def main():\n    if foo(`x):\n        y = 1\n    z = 2\n  w = 3\n";

    /// <summary>A5: a bracket opened on a dropped line before the abort, a column-0 statement below.</summary>
    internal const string A5 = "def main():\n    x = foo($,\ndef g():\n  return 1\n";

    /// <summary>A6: the line after an error recovery, misindented to a width on no level.</summary>
    internal const string A6 = "def main():\n    x = $\n      y = 1\n";

    /// <summary>A6b: A6 with a clean line between (the control).</summary>
    internal const string A6b = "def main():\n    x = $\n    q = 1\n      y = 1\n";

    /// <summary>A7: a bracket opened in the given-up text, its continuation at width 10, a misindented line below.</summary>
    internal const string A7 = "def main():\n    x = $ foo(1,\n          2)\n  y = 1\n";

    /// <summary>A9: a backslash-continued dropped line (the backslash arm; no bracket left open).</summary>
    internal const string A9 = "def main():\n    y = 1 + $ \\\n          2\n  z = 1\n";

    /// <summary>A11: a module-level bracket open to the end of the source above a class.</summary>
    internal const string A11 = "x = (\nclass Foo:\n    def m(self):\n        pass\n";

    /// <summary>A12: a bracket open to the end of the source, a misindented line below it (the lexer reads it inside the bracket).</summary>
    internal const string A12 = "def main():\n    xs = [1,\n    y = 2\n  z = 3\n";

    /// <summary>W1: the 8-space twin of A6 with a line below (verify round @ 4562b1d01).</summary>
    internal const string W1 = "def main():\n        _ = $\n          y = 1\n        print(x)\n";

    /// <summary>W3: A6 with a line one level deeper below the recovery line.</summary>
    internal const string W3 = "def main():\n    _ = $\n      y = 1\n        print(x)\n";

    /// <summary>F1 (P2.2, lead ruling L5): an 8-space document with a bracket open to the end above two statements.</summary>
    internal const string F1 = "def main():\n        _ = [1,\n        x = 1\n        print(x)\n";

    /// <summary>Verifier 1a: the recovery line two spaces deeper than a dropped block header (lead ruling L8).</summary>
    internal const string Verifier1a = "def main():\n    if foo($):\n      y = 1\n";

    /// <summary>Verifier 1c: a misindented line above an opener left open, its tail at the opener's 8 (lead ruling L9).</summary>
    internal const string Verifier1c = "def main():\n          q = 1\n        _ = [1,\n        x = 1\n";

    /// <summary>Verifier 1d: a call inside a replacement field abandoned at <c>$</c>, the field and the string closed (lead ruling L10).</summary>
    internal const string Verifier1d = "def main():\n    if c:\n        x = f\"{foo($)}\"\n    print(x)\n";

    /// <summary>A8: a block header whose colon is in the text the lexer gives up, its body at width 10 (lead ruling L6).</summary>
    internal const string A8 = "def main():\n    if foo($):\n          y = 1\n";

    /// <summary>
    /// A8 with an UNRELATED misindented line above the header (lead ruling L6's arbiter row): clause 6c is check-only, so
    /// the full candidate moves <c>y</c> with <c>q</c> and is refused whole — the unrelated repair is withheld (no edit
    /// @ ac3235c2a too). A builder skip would keep it, at the cost of the literal-loss guards (see IndentMap.UnplacedLines).
    /// </summary>
    internal const string A8q = "def main():\n      q = 1\n    if foo($):\n          y = 1\n";

    /// <summary>The edits of one route cell: <c>full</c>, <c>range-whole</c>, <c>range-line N</c> or <c>ontype N</c> (0-based N).</summary>
    private IReadOnlyList<TextEdit> RouteEdits(string document, string route) => RouteCell(document, route).Edits;

    private (IReadOnlyList<TextEdit> Edits, string Applied) RouteCell(string document, string route)
    {
        var lines = Compiler.Formatting.LineDiff.Split(document).Lines;
        var parts = route.Split(' ');
        return parts[0] switch
        {
            "full" => _driver.Full(document),
            "range-whole" => _driver.Range(document, Lines(0, lines.Count - 1)),
            "range-line" => _driver.Range(document, ClientLineRange(document, int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture))),
            "ontype" => _driver.OnType(document, int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)),
            _ => throw new ArgumentOutOfRangeException(nameof(route), route, null),
        };
    }

    /// <summary>Line <paramref name="line"/> as the client counts lines (<c>\r\n</c>, <c>\n</c> or a lone <c>\r</c>).</summary>
    private static LspRange ClientLineRange(string text, int line) =>
        new(new Position(line, 0), new Position(line, Compiler.Formatting.LineDiff.Split(text).Lines[line].Length));

    /// <summary><paramref name="text"/> with its 0-based <paramref name="lines"/> re-indented to <paramref name="indent"/>, line breaks kept.</summary>
    private static string Reindented(string text, string indent, params int[] lines)
    {
        var (split, breaks) = Compiler.Formatting.LineDiff.Split(text);
        var result = new System.Text.StringBuilder();
        for (var i = 0; i < split.Count; i++)
        {
            result.Append(lines.Contains(i) ? indent + split[i].TrimStart() : split[i]);
            if (i < breaks.Count)
                result.Append(breaks[i]);
        }

        return result.ToString();
    }

    /// <summary>
    /// The cells that apply no edit, each with the text the route would apply without its refusal (the @ ac3235c2a
    /// text where the route applied one: the direction "wrong edit → no edit" or, for a repair below an abandoned
    /// bracket, R-FU's "repair → no edit") and the clause that refuses it. A1, A2, A11, A12 (a real bracket open to
    /// the end) and A3, A4, A5, A7 (an abandoned one): clause 7 — every line below the opener is the bracket's. W1 on
    /// the line routes: clause 6b; W2 (<see cref="WideCloserLineAbort"/>) and W3: clause 6. A9 (the backslash arm;
    /// outside R-FU's bracket contract): the asymmetry that refused it @ ac3235c2a.
    /// </summary>
    public static TheoryData<string, string, string, string> NoEditCells => new()
    {
        // A1: @ ac3235c2a full / range-whole / range-line 4 moved print(y) out of its `if` (wrong edit → no edit);
        // on-type 4 applied nothing (a continuation line), and the line is below the opener now as well.
        { "A1", A1, "full", WithLine(A1, 4, "    print(y)") },
        { "A1", A1, "range-whole", WithLine(A1, 4, "    print(y)") },
        { "A1", A1, "range-line 4", WithLine(A1, 4, "    print(y)") },
        { "A1", A1, "ontype 4", WithLine(A1, 4, "    print(y)") },
        // A2: @ ac3235c2a full and range-whole moved both `pass` lines to column 0 (wrong edit → no edit).
        { "A2", A2, "full", "@[foo\nclass Bar:\npass\n\ndef main():\npass\n" },
        { "A2", A2, "range-whole", "@[foo\nclass Bar:\npass\n\ndef main():\npass\n" },
        // A3/A4: @ ac3235c2a full / range-whole / range-line 2 moved `y = 1` out of its `if` (wrong edit → no edit);
        // on-type 4 / range-line 4 repaired `  w = 3` below the abandoned bracket (repair → no edit, R-FU).
        { "A3", A3, "full", WithLine(A3, 2, "    y = 1") },
        { "A3", A3, "range-whole", WithLine(A3, 2, "    y = 1") },
        { "A3", A3, "range-line 2", WithLine(A3, 2, "    y = 1") },
        { "A3", A3, "ontype 2", WithLine(A3, 2, "    y = 1") },
        { "A3", A3, "ontype 4", WithLine(A3, 4, "    w = 3") },
        { "A3", A3, "range-line 4", WithLine(A3, 4, "    w = 3") },
        { "A4", A4, "full", WithLine(A4, 2, "    y = 1") },
        { "A4", A4, "range-whole", WithLine(A4, 2, "    y = 1") },
        { "A4", A4, "range-line 2", WithLine(A4, 2, "    y = 1") },
        { "A4", A4, "ontype 2", WithLine(A4, 2, "    y = 1") },
        { "A4", A4, "ontype 4", WithLine(A4, 4, "    w = 3") },
        { "A4", A4, "range-line 4", WithLine(A4, 4, "    w = 3") },
        // A5: @ ac3235c2a full / range-whole moved `def g():` into main (wrong edit → no edit); on-type 3 repaired
        // `  return 1` below the abandoned bracket (repair → no edit, R-FU).
        { "A5", A5, "full", "def main():\n    x = foo($,\n    def g():\n    return 1\n" },
        { "A5", A5, "range-whole", "def main():\n    x = foo($,\n    def g():\n    return 1\n" },
        { "A5", A5, "ontype 3", WithLine(A5, 3, "    return 1") },
        // A7: no edits @ ac3235c2a too (the continuation at width 10 refused the whole repair); below the opener now.
        { "A7", A7, "full", WithLine(WithLine(A7, 2, "    2)"), 3, "    y = 1") },
        { "A7", A7, "ontype 2", WithLine(A7, 2, "    2)") },
        { "A7", A7, "ontype 3", WithLine(A7, 3, "    y = 1") },
        // A9 on-type 2: the backslash continuation `2` (dropped whole by SPY0013 after the continued recovery) is the
        // dropped statement's continuation, not a logical start — the route refuses it as one (the check would align
        // it to the block's level, as it aligns any continuation). A9 full now repairs: see RepairCells.
        { "A9", A9, "ontype 2", WithLine(A9, 2, "    2") },
        // A11: @ ac3235c2a full moved `def m` and `pass` to column 0 (wrong edit → no edit).
        { "A11", A11, "full", "x = (\nclass Foo:\ndef m(self):\npass\n" },
        // A12: @ ac3235c2a full "repaired" `  z = 3`, a line the lexer reads inside the bracket (wrong edit → no edit).
        { "A12", A12, "full", WithLine(A12, 3, "    z = 3") },
        { "A12", A12, "ontype 3", WithLine(A12, 3, "    z = 3") },
        // W1: the recovery line repaired ALONE to 4 spaces under an 8-space body — no edit @ ac3235c2a and now
        // (clause 6b: the applied width 4 is on no level of [0, 8]; the lexer reads print(x) one block deeper).
        { "W1", W1, "ontype 2", WithLine(W1, 2, "    y = 1") },
        { "W1", W1, "range-line 2", WithLine(W1, 2, "    y = 1") },
        // W2: the recovery line after an 8-space closer line re-indented alone — clause 6 (no edit @ ac3235c2a and now).
        { "W2", WideCloserLineAbort, "ontype 5", WithLine(WideCloserLineAbort, 5, "    print(s)") },
        // W3: the recovery line repaired with a deeper line below it — clause 6 (print(x) depth 3 → 2; no edit @ ac3235c2a and now).
        { "W3", W3, "ontype 2", WithLine(W3, 2, "    y = 1") },
        // F1 (lead rulings L5, L9): @ ac3235c2a full / range-whole aligned all three lines to 4 (lines below the opener
        // included); the frozen tail now stays at 8 and the opener moves only with it — clause 7 (alignment → no edit).
        { "F1", F1, "full", WithLine(F1, 1, "    _ = [1,") },
        { "F1", F1, "range-whole", WithLine(F1, 1, "    _ = [1,") },
        // A8 (lead ruling L6): the recovery line a level or more deeper than its dropped header — its level is unknown
        // (the colon is in the given-up text). No edit @ ac3235c2a and now; clause 6c refuses the candidate (at width 8
        // clause 6 does too). The 8-space twin: the header at 8, the body at 18.
        { "A8@8", A8.Replace("          y", "        y", StringComparison.Ordinal), "full", WithLine(A8, 2, "    y = 1") },
        { "A8@8", A8.Replace("          y", "        y", StringComparison.Ordinal), "ontype 2", WithLine(A8, 2, "    y = 1") },
        { "A8@8", A8.Replace("          y", "        y", StringComparison.Ordinal), "range-line 2", WithLine(A8, 2, "    y = 1") },
        { "A8@10", A8, "full", WithLine(A8, 2, "    y = 1") },
        { "A8@10", A8, "ontype 2", WithLine(A8, 2, "    y = 1") },
        { "A8@10", A8, "range-line 2", WithLine(A8, 2, "    y = 1") },
        { "A8@12", A8.Replace("          y", "            y", StringComparison.Ordinal), "full", WithLine(A8, 2, "    y = 1") },
        { "A8@12", A8.Replace("          y", "            y", StringComparison.Ordinal), "ontype 2", WithLine(A8, 2, "    y = 1") },
        { "A8@12", A8.Replace("          y", "            y", StringComparison.Ordinal), "range-line 2", WithLine(A8, 2, "    y = 1") },
        { "A8 wide", "def main():\n        if foo($):\n                  y = 1\n", "full", "def main():\n    if foo($):\n    y = 1\n" },
        { "A8 wide", "def main():\n        if foo($):\n                  y = 1\n", "ontype 2", "def main():\n        if foo($):\n    y = 1\n" },
        { "A8 wide", "def main():\n        if foo($):\n                  y = 1\n", "range-line 2", "def main():\n        if foo($):\n    y = 1\n" },
        // A8q (L6's arbiter row): full / range-whole move `y` with the unrelated `q` — refused whole, `q`'s repair withheld.
        { "A8q", A8q, "full", WithLine(WithLine(A8q, 1, "    q = 1"), 3, "    y = 1") },
        { "A8q", A8q, "range-whole", WithLine(WithLine(A8q, 1, "    q = 1"), 3, "    y = 1") },
    };

    [Theory]
    [MemberData(nameof(NoEditCells))]
    public void P22h_TheRoutesApplyNothing_AndTheCheckRefusesWhatTheyWouldApply(string name, string document, string route, string candidate)
    {
        RouteEdits(document, route).Should().BeEmpty("{0} {1}", name, route);
        candidate.Should().NotBe(document, "{0} {1}: the candidate is an edit", name, route);
        if (RefusedAsAContinuationLine(document, route))
            return;
        FormattingFallback.IndentOnlyPreserved(document, candidate).Should().BeFalse("{0} {1}: the check refuses the candidate", name, route);
    }

    /// <summary>
    /// Whether a line route's refusal is the handler's own rule — the requested line is not a logical-line start (a
    /// bracket or backslash continuation, or the dropped continuation of a partially read line) — rather than the check's:
    /// the check aligns a continuation line to its block's level like any other, so it is not what refuses the cell.
    /// </summary>
    private static bool RefusedAsAContinuationLine(string document, string route)
    {
        if (route is "full" or "range-whole")
            return false;
        var line = int.Parse(route.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
        return !IndentationService.BuildIndentMap(document).LogicalLineStarts.Contains(line + 1);
    }

    /// <summary>
    /// The cells that apply a repair, with the text each applies. A1b: the misindented line ABOVE the opener keeps its
    /// repair on full, range-whole and on-type 1 (full @ ac3235c2a also moved print(y) — the arm that is gone). A6, W1
    /// full: the line after an error recovery is a logical start and is repaired (no edit @ ac3235c2a → repair). A6b:
    /// unchanged (one clean line between). A9 on-type 3: the line after the backslash continuation is a logical start in
    /// the source and, now, in the applied text too (no edit @ ac3235c2a → repair: the asymmetry that refused it is gone).
    /// </summary>
    public static TheoryData<string, string, string, string> RepairCells => new()
    {
        { "A1b", A1b, "full", WithLine(A1b, 1, "    x = 1") },
        { "A1b", A1b, "range-whole", WithLine(A1b, 1, "    x = 1") },
        { "A1b", A1b, "ontype 1", WithLine(A1b, 1, "    x = 1") },
        { "A6", A6, "full", WithLine(A6, 2, "    y = 1") },
        { "A6", A6, "range-whole", WithLine(A6, 2, "    y = 1") },
        { "A6", A6, "ontype 2", WithLine(A6, 2, "    y = 1") },
        { "A6", A6, "range-line 2", WithLine(A6, 2, "    y = 1") },
        { "A6b", A6b, "full", WithLine(A6b, 3, "    y = 1") },
        { "A6b", A6b, "ontype 3", WithLine(A6b, 3, "    y = 1") },
        { "A9", A9, "ontype 3", WithLine(A9, 3, "    z = 1") },
        // A9 full (/verify-implementation of plan-f92797): the backslash continuation `2`, dropped whole by SPY0013 after
        // the continued recovery, is the dropped statement's continuation (the map keys the dropped physical line of a
        // partially read logical line to that line's start) — aligned to the block's level like any continuation — and
        // `z = 1` is repaired into main's body. No edit at 4562b1d01 and at 040c79ec2 (the plan's ledgered lost repair) →
        // repair; no statement changes block.
        { "A9", A9, "full", WithLine(WithLine(A9, 2, "    2"), 3, "    z = 1") },
        { "W1", W1, "full", "def main():\n    _ = $\n    y = 1\n    print(x)\n" },
        // A8q on-type 1: the unrelated line alone is repaired (the recovery line is not in the candidate).
        { "A8q", A8q, "ontype 1", WithLine(A8q, 1, "    q = 1") },
    };

    /// <summary>F1's 4-space twin (lead ruling L5): the bracket left open, every line already on its level — no edit before or after.</summary>
    [Fact]
    public void P22h_F1FourSpaceTwin_IsUnchanged()
    {
        const string twin = "def main():\n    _ = [1,\n    x = 1\n    print(x)\n";
        FormattingFallback.ReindentDocument(twin).Should().Be(twin);
        RouteEdits(twin, "full").Should().BeEmpty();
        RouteEdits(twin, "range-whole").Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(RepairCells))]
    public void P22h_TheRoutesApplyTheRepair(string name, string document, string route, string repaired)
    {
        RouteCell(document, route).Applied.Should().Be(repaired, "{0} {1}", name, route);
    }

    /// <summary>The CRLF and lone-CR twins of A1 (every route: no edits) and A6 (the recovery line repaired, its line break kept).</summary>
    [Theory]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void P22h_LineBreakTwins_A1Frozen_A6Repaired(string lineBreak)
    {
        var a1 = A1.Replace("\n", lineBreak, StringComparison.Ordinal);
        FormattingFallback.IndentOnlyPreserved(a1, Reindented(a1, "    ", 4)).Should().BeFalse();
        foreach (var route in new[] { "full", "range-whole", "range-line 4", "ontype 4" })
            RouteEdits(a1, route).Should().BeEmpty(route);

        var a6 = A6.Replace("\n", lineBreak, StringComparison.Ordinal);
        var repaired = Reindented(a6, "    ", 2);
        foreach (var route in new[] { "full", "range-whole", "range-line 2", "ontype 2" })
            RouteCell(a6, route).Applied.Should().Be(repaired, route);
    }

    /// <summary>
    /// Clause 7 at the check seam (R-FU): a text that repairs the line above the opener AND re-indents one below it
    /// (A1b's @ ac3235c2a full text: <c>print(y)</c> moved out of its <c>if</c>) is refused; the same text with the line
    /// below untouched is accepted. Every other clause accepts the refused text — <c>print(y)</c> is the bracket's
    /// continuation in both maps — so it discriminates clause 7 alone.
    /// </summary>
    [Fact]
    public void IndentOnlyPreserved_ALineBelowAnOpenBracket_IsRefused()
    {
        IndentationService.BuildIndentMap(A1b).OpenBracketLine.Should().Be(3);
        FormattingFallback.IndentOnlyPreserved(A1b, WithLine(WithLine(A1b, 1, "    x = 1"), 5, "    print(y)")).Should().BeFalse();
    }

    [Fact]
    public void IndentOnlyPreserved_ALineBelowAnOpenBracket_Untouched_IsAccepted()
    {
        FormattingFallback.IndentOnlyPreserved(A1b, WithLine(A1b, 1, "    x = 1")).Should().BeTrue();
    }

    /// <summary>
    /// Clause 6b at the check seam: W1's line-route text — the recovery line repaired ALONE to 4 spaces under an 8-space
    /// body — lands on no level of the applied stack <c>[0, 8]</c>, while the lexer, whose stack restarts after the
    /// recovery, would read <c>print(x)</c> one block deeper. Clause 6 exempts the line (its source width 10 is
    /// dropped) and every other clause accepts the text, so it discriminates clause 6b alone.
    /// </summary>
    [Fact]
    public void IndentOnlyPreserved_ARepairedLineOnNoLevel_IsRefused()
    {
        FormattingFallback.IndentOnlyPreserved(W1, WithLine(W1, 2, "    y = 1")).Should().BeFalse();
    }

    /// <summary>
    /// The opener moves only with its tail (lead ruling L9; L5's clause 7b case): F1's opener moved to 4 spaces alone,
    /// its tail left at 8, is refused — the opener line is frozen with a non-blank tail (clause 7).
    /// </summary>
    [Fact]
    public void IndentOnlyPreserved_AFrozenOpenerMoved_IsRefused()
    {
        IndentationService.BuildIndentMap(F1).FrozenFrom.Should().Be(2);
        FormattingFallback.IndentOnlyPreserved(F1, WithLine(F1, 1, "    _ = [1,")).Should().BeFalse();
    }

    /// <summary>
    /// Verifier 1c (lead ruling L9) at the check seam: <c>q</c> at width 10 repaired to 4 above an opener frozen at 8
    /// strands the opener one block deeper than <c>q</c> once the bracket closes. Clause 6 exempts the opener (its
    /// source width reads on no level: the width rule pushed 10) and clause 7 sees it unchanged; clause 6b judges the
    /// frozen opener and refuses its new push after a line that opens no block — it discriminates that test alone.
    /// </summary>
    [Fact]
    public void IndentOnlyPreserved_ALineAboveAFrozenOpenerRepairedSoTheOpenerIsStranded_IsRefused()
    {
        FormattingFallback.IndentOnlyPreserved(Verifier1c, WithLine(Verifier1c, 1, "    q = 1")).Should().BeFalse();
    }

    /// <summary>
    /// Its accepting twin (the verifier's G3: re-spelled so it also asserts what the routes apply): <c>q</c> repaired to
    /// the opener's 8 lands on the opener's level — accepted. No route builds that text (the map's level for <c>q</c>
    /// is 4 spaces), and every route applies nothing to 1c.
    /// </summary>
    [Fact]
    public void IndentOnlyPreserved_ALineAboveAFrozenOpenerRepairedToItsLevel_IsAccepted_TheRoutesApplyNothing()
    {
        FormattingFallback.IndentOnlyPreserved(Verifier1c, WithLine(Verifier1c, 1, "        q = 1")).Should().BeTrue();
        foreach (var route in new[] { "full", "range-whole", "ontype 1", "range-line 1" })
            RouteEdits(Verifier1c, route).Should().BeEmpty(route);
    }

    /// <summary>
    /// Clause 6c's L8 arm at the check seam: 1a's putative body moved to its header's width is refused (the dropped
    /// <c>if foo($):</c> could open a block — the lexer read a depth-0 <c>:</c>); the accepting twin is A6, whose dropped
    /// <c>x = $</c> could not, and whose recovery line keeps its repair.
    /// </summary>
    [Fact]
    public void IndentOnlyPreserved_APutativeBodyBelowAPossibleHeaderMoved_IsRefused()
    {
        var map = IndentationService.BuildIndentMap(Verifier1a);
        map.RecoveryLines.Should().ContainKey(3).WhoseValue.Should().Be(new RecoveryLine(2, 4, PossibleHeader: true));
        map.UnplacedLines.Should().BeEquivalentTo(new[] { 2, 3 });
        FormattingFallback.IndentOnlyPreserved(Verifier1a, WithLine(Verifier1a, 2, "    y = 1")).Should().BeFalse();

        IndentationService.BuildIndentMap(A6).UnplacedLines.Should().BeEmpty();
        FormattingFallback.IndentOnlyPreserved(A6, WithLine(A6, 2, "    y = 1")).Should().BeTrue();
    }

    /// <summary>
    /// Clause 6c at the check seam (lead ruling L6): A8's recovery line moved to 4 spaces — out of the <c>if</c> whose
    /// colon the lexer never read — is refused; clause 6 exempts its SPY0013 width and 4 lands on a level, so it
    /// discriminates clause 6c alone. The map reads the line as a recovery line of a dropped line at width 4.
    /// </summary>
    [Fact]
    public void IndentOnlyPreserved_ARecoveryLineALevelDeeperMoved_IsRefused()
    {
        IndentationService.BuildIndentMap(A8).RecoveryLines.Should().ContainKey(3).WhoseValue.Should().Be(new RecoveryLine(2, 4, PossibleHeader: true));
        FormattingFallback.IndentOnlyPreserved(A8, WithLine(A8, 2, "    y = 1")).Should().BeFalse();

        // the 8-space twin's header moved to 4 alone, its body left at 18: the pair's relation changes the same way
        const string wide = "def main():\n        if foo($):\n                  y = 1\n";
        FormattingFallback.IndentOnlyPreserved(wide, WithLine(wide, 1, "    if foo($):")).Should().BeFalse();
    }

    /// <summary>Clause 6c's accepting twin: A6's recovery line, two spaces deeper than its dropped line, is repaired.</summary>
    [Fact]
    public void IndentOnlyPreserved_ARecoveryLineLessThanALevelDeeperRepaired_IsAccepted()
    {
        IndentationService.BuildIndentMap(A6).RecoveryLines.Should().ContainKey(3).WhoseValue.Should().Be(new RecoveryLine(2, 4, PossibleHeader: false));
        FormattingFallback.IndentOnlyPreserved(A6, WithLine(A6, 2, "    y = 1")).Should().BeTrue();
    }

    /// <summary>Clause 6b's accepting twin: the 4-space identity text — the repaired line lands on the level 4 of <c>[0, 4]</c>.</summary>
    [Fact]
    public void IndentOnlyPreserved_ARepairedLineOnALevel_IsAccepted()
    {
        const string identity = "def main():\n    _ = $\n      y = 1\n    print(x)\n";
        FormattingFallback.IndentOnlyPreserved(identity, WithLine(identity, 2, "    y = 1")).Should().BeTrue();
    }

    /// <summary>
    /// Lead ruling L4: a recovery line that opens a literal spanning lines is the literal-loss family's — re-indented to a
    /// width on no level it is dropped with its opener (SPY0013), the lexer loses the literal (<c>DroppedOpener</c>) and
    /// every route refuses (no edits @ ac3235c2a and now): the repair of the opener line is withheld and the literal's
    /// lines are untouched. In the route-parity sweep such a cell is exempt by clean-twin parity (lead ruling L7: with
    /// <c>_ = 0</c> in place of <c>_ = $</c> the line is withheld too) and counted in <c>S7ParityExemptPin</c>.
    /// </summary>
    [Fact]
    public void L4_ARecoveryLineOpeningALiteral_IsWithheld_TheLiteralLinesUntouched()
    {
        const string document = "def main():\n    _ = $\n      s = \"\"\"\n        key: value\n    \"\"\"\n    print(s)\n";
        IndentationService.BuildIndentMap(document).LiteralStateUnknown.Should().BeTrue("the dropped line takes the literal's opener with it");
        FormattingFallback.ReindentDocument(document).Should().NotBe(document, "the unchecked candidate re-indents the lines");
        foreach (var route in new[] { "full", "range-whole", "range-line 2", "ontype 2", "range-line 3" })
            RouteEdits(document, route).Should().BeEmpty(route);
    }

    // ---- P22h Phase 3 (#2280, R-FV): a closer line ending in a literal that aborts inside itself. Arm (b) of the
    // re-paired-closer fact counts a quote in the given-up text only when it leaves a literal open at the line's end (the
    // close-line walk), so the aborted literal's OWN delimiter no longer sets the fact. Measured over stdio @ 2c60c8dee
    // against the base LSP (4562b1d01 = be1c16fcf's; .claude/tmp/p22h-impl/p3b/out_base.txt, out_head.txt).

    /// <summary>The nine own-quote tails (plan-f92797 Design 4), the three the walk cannot pair last, and the quote-free control.</summary>
    private static readonly (string Name, string Tail)[] OwnQuoteTails =
    {
        ("dq", "\"\\q\""), ("oq", "'\\q'"), ("bq", "b\"\\q\""), ("fconv", "f\"{x!q}\""), ("tconv", "t\"{x!q}\""), ("fbrace", "f\"}\""),
        ("fspec", "f\"{x:\">}\""), ("bt", "`it's"), ("cmt", "$  # it's"),
    };

    /// <summary>Why each of the three tails the close-line walk cannot pair keeps the fact set (R-FV's "(b) stacks under (a)").</summary>
    private static readonly Dictionary<string, string> LimitReason = new()
    {
        ["fspec"] = "#2274 known limit: the spec-quote fork keeps 'either walk', and reading the quote as the f-string's closer leaves a quote open",
        ["bt"] = "#2274 known limit: an unterminated backtick name's text is given up with the line, its apostrophe counts",
        ["cmt"] = "#2274 known limit: the remainder after '#' is a comment to the lexer and string text to the user, its apostrophe counts",
    };

    private static readonly string[] Routes5 = { "full", "ontype 5", "range-line 5" };
    private static readonly string[] Routes3 = { "full", "ontype 3", "range-line 3" };

    /// <summary>B-tails: no stray; the closer line of <c>s</c> ends in <c> + tail</c>; <c>print(s)</c> at 6 spaces is the repair (line 5).</summary>
    private static string NoStrayTail(string tail) => "def main():\n    s = \"\"\"\n      a\n    \"\"\" + " + tail + "\n    q = 1\n      print(s)\n";

    /// <summary>B-stray-closed: a stray triple above re-pairs; the orphan line <c>_ = '""" + tail'</c> closes its quote (line 5).</summary>
    private static string StrayClosedTail(string tail) => "\"\"\"\ndef main():\n    s = \"\"\"\n      key: value\n    \"\"\"\n    _ = '\"\"\" + " + tail + "'\n    print(s)\n";

    /// <summary>B-stray-typing: the same orphan still being typed — its closing quote missing.</summary>
    private static string StrayTypingTail(string tail) => "\"\"\"\ndef main():\n    s = \"\"\"\n      key: value\n    \"\"\"\n    _ = '\"\"\" + " + tail + "\n    print(s)\n";

    /// <summary>The text a route would apply without its refusal: the full fallback's candidate, or the line re-indented to its map level.</summary>
    private static string UncheckedCandidate(string document, string route)
    {
        var parts = route.Split(' ');
        return parts[0] == "full"
            ? FormattingFallback.ReindentDocument(document)
            : RangeCandidate(document, int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    public static TheoryData<string, string, string, bool> CloseLineOwnQuoteTailCells()
    {
        var data = new TheoryData<string, string, string, bool>();
        foreach (var (name, tail) in OwnQuoteTails)
        {
            var limit = LimitReason.TryGetValue(name, out var reason);
            foreach (var route in Routes5)
                data.Add(limit ? $"{name} — {reason}" : $"{name} — repaired (no edit @ be1c16fcf)", tail, route, !limit);
        }

        return data;
    }

    /// <summary>
    /// #2280's cells: no stray; the closer line aborts inside its own short string or f-string (SPY0004, SPY0030,
    /// SPY0021), the given-up text's quotes pair, nothing is lost — every route applies exactly the repair of line 5
    /// (<c>      print(s)</c> → <c>    print(s)</c>; @ be1c16fcf no edits: the aborted literal's own delimiter set the
    /// fact). The three tails the walk cannot pair keep the fact and the lost repair, pinned as #2274's known limits (the
    /// reason is in the display name); their unchecked candidate is the repair, so the refusal is what withholds it.
    /// </summary>
    [Theory]
    [MemberData(nameof(CloseLineOwnQuoteTailCells))]
    public void CloseLineOwnQuoteTail_NoStray_EveryRouteRepairs_ExceptTheThreeKnownLimits(string name, string tail, string route, bool repaired)
    {
        var document = NoStrayTail(tail);
        if (repaired)
        {
            RouteCell(document, route).Applied.Should().Be(WithLine(document, 5, "    print(s)"), "{0} {1}", name, route);
            return;
        }

        RouteEdits(document, route).Should().BeEmpty("{0} {1}", name, route);
        UncheckedCandidate(document, route).Should().Be(WithLine(document, 5, "    print(s)"), "{0} {1}: the candidate is the repair the fact withholds", name, route);
    }

    public static TheoryData<string, string, string> StrayClosedTailCells()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var (name, tail) in OwnQuoteTails.Append(("dollar control", "$")))
        {
            foreach (var route in Routes3)
                data.Add(name, tail, route);
        }

        return data;
    }

    /// <summary>
    /// The positive controls (R-FV: every new tail has its stray-bearing twin): with a stray triple above and the orphan's
    /// closing quote typed, the orphan line's text after the re-paired closer holds an unpaired quote whatever the tail —
    /// the fact is set and no route rewrites <c>      key: value</c> (string content). No edits @ be1c16fcf and now, for
    /// the nine tails and the quote-free <c>$</c> control; the unchecked candidate rewrites the string line.
    /// </summary>
    [Theory]
    [MemberData(nameof(StrayClosedTailCells))]
    public void CloseLineOwnQuoteTail_StrayClosed_EveryRouteRefuses(string name, string tail, string route)
    {
        var document = StrayClosedTail(tail);
        RouteEdits(document, route).Should().BeEmpty("{0} {1}", name, route);
        UncheckedCandidate(document, route).Split('\n')[3].Should().NotBe(document.Split('\n')[3], "{0} {1}: the candidate rewrites the string line", name, route);
    }

    public static TheoryData<string, string, string, string?> OrphanBeingTypedCells()
    {
        var data = new TheoryData<string, string, string, string?>();
        foreach (var (name, tail) in OwnQuoteTails.Append(("dollar", "$")))
        {
            var document = StrayTypingTail(tail);
            var limit = LimitReason.ContainsKey(name);
            foreach (var route in Routes3)
            {
                var rewritten = route == "full"
                    ? WithLine(WithLine(WithLine(document, 3, "key: value"), 4, "\"\"\""), 6, "print(s)")
                    : WithLine(document, 3, "key: value");
                data.Add(limit ? $"{name} — refused (the walk cannot pair it)"
                    : name == "dollar" ? "dollar — #2274 pre-existing wrong edit, measured @ be1c16fcf: `$` rewrote it"
                    : $"{name} — #2274 class, no edit @ be1c16fcf → wrong edit (the tail's own quotes pair, as `$` has none)",
                    tail, route, limit ? null : rewritten);
            }
        }

        return data;
    }

    /// <summary>
    /// A PIN OF A KNOWN WRONG EDIT, not an endorsement (#2274, R-FQ's permanent known-limit tracker): the orphan still
    /// being typed — <c>_ = '""" + tail</c>, its closing quote missing — has no lexer signature once the tail's own quotes
    /// pair (the stray is the only difference, as R-FQ names), so the re-paired closer goes unrecorded and every route
    /// rewrites <c>      key: value</c> (string content), exactly as the quote-free <c>$</c> member of the class already
    /// did (measured @ be1c16fcf: <c>$</c> rewrote it on full, range-line 3 and on-type 3). R-FV's accepted trade: the six
    /// pairing tails move from refused-by-accident (the old rule saw the tail's own quote) to this class. The three tails
    /// the walk cannot pair stay refused. A change that refuses these is a fix of #2274 — record it there and update the pin.
    /// </summary>
    [Theory]
    [MemberData(nameof(OrphanBeingTypedCells))]
    public void KnownLimit2274_OrphanBeingTypedBeforeAnOwnQuoteTail_RewritesTheStringLine(string name, string tail, string route, string? applied)
    {
        var document = StrayTypingTail(tail);
        if (applied == null)
        {
            RouteEdits(document, route).Should().BeEmpty("{0} {1}", name, route);
            UncheckedCandidate(document, route).Split('\n')[3].Should().NotBe(document.Split('\n')[3], "{0} {1}: the candidate rewrites the string line", name, route);
            return;
        }

        RouteCell(document, route).Applied.Should().Be(applied, "{0} {1}", name, route);
    }

    // ---- P22h remediation R2 (the standalone verifier's 1a–1e, lead rulings L8–L10). Measured over stdio, base LSP
    // (4562b1d01) vs HEAD (f4b149467 + this change): .claude/tmp/p22h-impl/r2/out_base.txt, out_head.txt.

    /// <summary>
    /// The verifier's documents, each with the base behaviour its rows record by direction. 1a and siblings (L8): the
    /// recovery line below a dropped line that could open a block — base no edit, HEAD before L8 moved the body out of its
    /// block on every route; no edit now. 1a-wide: base on-type 1 / range-line 1 re-indented the header alone (8 → 4)
    /// off its body — no edit now. 1b (L8, two tabs) and 1b-nohdr (L6 measured in columns: a dropped line with no
    /// colon, two tabs are 16 columns): base no edit. 1c and its abandoned twin (L9): base aligned every line (full / range-whole)
    /// or re-indented one alone (on-type, range-line); no edit now — the opener moves only with its tail and no repair of
    /// <c>q</c> leaves it on a level. 1d (L10): base moved <c>print(x)</c> into the <c>if</c> on full, range-whole and
    /// range-line 3; no edit now — the line after the dropped one starts a logical line and is judged.
    /// </summary>
    public static readonly Dictionary<string, string> R2NoEditDocuments = new()
    {
        ["1a@5"] = "def main():\n    if foo($):\n     y = 1\n",
        ["1a@6"] = Verifier1a,
        ["1a@7"] = "def main():\n    if foo($):\n       y = 1\n",
        ["1a tab"] = "def main():\n    if foo($):\n\ty = 1\n",
        ["1a CRLF"] = "def main():\r\n    if foo($):\r\n      y = 1\r\n",
        ["1a while"] = "def main():\n    while $:\n      y = 1\n",
        ["1a elif"] = "def main():\n    if a:\n        x = 1\n    elif foo($):\n      y = 1\n",
        ["1a for"] = "def main():\n    for i in range($):\n      a = i\n      b = a\n    print(b)\n",
        ["1a def m"] = "class C:\n    def m(self, $):\n      return 1\n",
        ["1a module if"] = "if foo($):\n  y = 1\n",
        ["1a if x: $"] = "def main():\n    if x: $\n      y = 1\n",
        ["1a wide"] = "def main():\n        if foo($):\n          y = 1\n",
        ["1b"] = "def main():\n    if foo($):\n\t\ty = 1\n    z = 2\n",
        // /verify-implementation of plan-f92797 (C1, C2): a header spanning two lines whose colon is on the dropped
        // CONTINUATION line, where the lexer read no token. The dropped physical line is the logical line's continuation
        // (the recovery-line judgment keys on the logical line's start); read as a dropped line of its own, it popped the
        // width stack and the body below was levelled out of its block on every route at 040c79ec2 (base: no edit).
        ["C1 for header, colon on the dropped continuation at column 0"] = "def main():\n    for i in range(1,\n$):\n        print(i)\n    print(0)\n",
        ["C1 while"] = "def main():\n    while foo(1,\n$):\n        print(1)\n    print(0)\n",
        ["C1 CRLF"] = "def main():\r\n    for i in range(1,\r\n$):\r\n        print(i)\r\n    print(0)\r\n",
        ["C1 wide"] = "def main():\n        for i in range(1,\n$):\n                print(i)\n        print(0)\n",
        ["C2 if header, colon on the dropped backslash continuation"] = "def main():\n    if a and \\\n$:\n        print(1)\n    print(0)\n",
        // The same header with `$):` at the opener's width and the body 2 deeper: 040c79ec2 "repaired" the body to 8 — a
        // level guessed under a header the lexer could not read (base: no edit); the body is a putative body, unplaced.
        ["C1 @4, body at 6"] = "def main():\n    for i in range(1,\n    $):\n      print(i)\n    print(0)\n",
        ["1b no header"] = "def main():\n    x = $\n\t\ty = 1\n    z = 2\n",
        ["1c"] = Verifier1c,
        ["1c abandoned"] = "def main():\n          q = 1\n        _ = foo($,\n        if q:\n            print(q)\n",
        ["1d"] = Verifier1d,
        ["1d list"] = "def main():\n    if c:\n        x = f\"{[$]}\"\n    print(x)\n",
        ["1e (R-FU direction: repaired at base)"] = "def main():\n    x = 1\n      foo(1,\n          2)\n    if x:\n          y = 2\n",

        // R5 (the second verifier, lead ruling L11). L6-SIB: the line after the recovery line, a level deeper than a
        // dropped line whose colon an unterminated string swallowed — base no edit; 158a02acd on-type 3 / range-line 3
        // moved it out of the block; no edit now (the putative body runs to the first line at or above the dropped width).
        ["L6-SIB"] = "def main():\n    if x == \"abc:\n          y = 1\n          z = 2\n    w = 3\n",
        ["L6-SIB CRLF"] = "def main():\r\n    if x == \"abc:\r\n          y = 1\r\n          z = 2\r\n    w = 3\r\n",
        ["L6-SIB for"] = "def main():\n    for c in \"abc:\n          print(c)\n          print(c)\n    w = 3\n",
        // L6-PLUS2: the same header with its body at +2 — base no edit; 158a02acd moved it on every route; no edit now
        // (L11(a): a dropped line whose end the lexer cannot read is a possible header).
        ["L6-PLUS2"] = "def main():\n    if x == \"abc:\n      y = 1\n      z = 2\n    w = 3\n",
        ["L6-PLUS2 f-string"] = "def main():\n    if f\"{x == y:\n      y = 1\n      z = 2\n    w = 3\n",
        // N-NEST: an inner dropped header deeper than the outer's body, then a line dedented to the outer body — base AND
        // 158a02acd moved `z = 2` out of the outer `if` (on-type / range-line); no edit now (the shallowest bound).
        ["N-NEST"] = "def main():\n    if foo($):\n            if bar($):\n                y = 1\n        z = 2\n    w = 3\n",
        ["N-NEST module"] = "if foo($):\n        if bar($):\n            y = 1\n    z = 2\nw = 3\n",
        // FF: a recovery line indented with a form feed (a vertical tab, a no-break space) — base no edit; 158a02acd
        // deleted the character and put `y = 1` at column 0 on every route. Its clean twin did so on base too.
        ["FF"] = "def main():\n    if foo($):\n\f      y = 1\n",
        ["FF vertical tab"] = "def main():\n    if foo($):\n\v      y = 1\n",
        ["FF no-break space"] = "def main():\n    if foo($):\n\u00a0      y = 1\n",
        ["FF clean twin (wrong edit at base)"] = "def main():\n    if c:\n\f        y = 1\n",

        // R7 (the third verifier's OW-LEVEL / OW-HEADER, lead ruling L12): a document with a line indented with other
        // whitespace gets no indent-only edit. Base made no edit on W1/W2/DB2c/DB2d (cb5303f73 moved z out of its block on
        // every route — the per-line guard let the line take its header's block); W3/FF5 on-type and range-line moved the
        // body on base too.
        ["W1"] = "def main():\n    if foo($):\n\f      y = 1\n      z = 2\n    w = 3\n",
        ["W1 CRLF"] = "def main():\r\n    if foo($):\r\n\f      y = 1\r\n      z = 2\r\n    w = 3\r\n",
        ["W1 no-break space"] = "def main():\n    if foo($):\n\u00a0     y = 1\n      z = 2\n    w = 3\n",
        ["W2"] = "def main():\n    if c:\n\f        y = 1\n        z = 2\n    w = 3\n",
        ["W2 CRLF"] = "def main():\r\n    if c:\r\n\f        y = 1\r\n        z = 2\r\n    w = 3\r\n",
        ["W2 no-break space"] = "def main():\n    if c:\n\u00a0       y = 1\n        z = 2\n    w = 3\n",
        ["W2 vertical tab"] = "def main():\n    if c:\n\v        y = 1\n        z = 2\n    w = 3\n",
        ["W1 vertical tab"] = "def main():\n    if foo($):\n\v      y = 1\n      z = 2\n    w = 3\n",
        ["W2b"] = "def main():\n    if c:\n\f        y = 1\n        z = 2\n",
        ["W3 (OW-HEADER, moved at base)"] = "def main():\n    if c:\n\f        if d:\n            y = 1\n    z = 2\n",
        ["FF5"] = "def main():\n    if c:\n\f        if foo($):\n            y = 1\n",
        ["DB2c"] = "def main():\n    if ready($):\n\f      y = 1\n      z = 2\n    w = 3\n",
        ["DB2d"] = "def main():\n    if ready($):\n\u00a0       y = 1\n        z = 2\n    w = 3\n",
        ["NBSP far from the repair (L12 cost)"] = "def main():\n    x = 1\n      y = 2\n    z = 3\n    a = 4\n    b = 5\n    c = 6\n    d = 7\n\u00a0   e = 8\n",
    };

    /// <summary>
    /// The text a line route applied at base or @ 158a02acd where the builders no longer make one: FF's lines indented
    /// with a character other than a space or a tab are never re-indented now (L11), so the candidate the check must
    /// refuse is the one those trees applied — the character deleted and <c>y = 1</c> at column 0.
    /// </summary>
    private static readonly Dictionary<string, string> R5MovedLineText = new()
    {
        ["FF"] = "def main():\n    if foo($):\ny = 1\n",
        ["FF vertical tab"] = "def main():\n    if foo($):\ny = 1\n",
        ["FF no-break space"] = "def main():\n    if foo($):\ny = 1\n",
        ["FF clean twin (wrong edit at base)"] = "def main():\n    if c:\ny = 1\n",
    };

    /// <summary>
    /// 1d's full / range-whole text at base (measured): <c>print(x)</c> moved into the <c>if</c>. HEAD's builder makes no
    /// candidate there (the line is now a judged logical line at its level), so the base text is the one the check refuses.
    /// </summary>
    private static readonly Dictionary<string, string> R2BaseFullText = new()
    {
        ["1d"] = WithLine(Verifier1d, 3, "        print(x)"),
        ["1d list"] = WithLine("def main():\n    if c:\n        x = f\"{[$]}\"\n    print(x)\n", 3, "        print(x)"),
    };

    /// <summary>
    /// The cells of <see cref="R2NoEditDocuments"/> with an edit to refuse: full and range-whole when the full builder's
    /// candidate is an edit (or base applied one, <see cref="R2BaseFullText"/>), and on-type / range-line on every line
    /// the range builder would re-indent.
    /// </summary>
    public static TheoryData<string, string> R2NoEditCells()
    {
        var data = new TheoryData<string, string>();
        foreach (var (name, document) in R2NoEditDocuments)
        {
            if (FormattingFallback.ReindentDocument(document) != document || R2BaseFullText.ContainsKey(name) || R5MovedLineText.ContainsKey(name))
            {
                data.Add(name, "full");
                data.Add(name, "range-whole");
            }
            var lines = Compiler.Formatting.LineDiff.Split(document).Lines;
            for (var l = 0; l < lines.Count; l++)
            {
                if (RangeCandidate(document, l) != document || (R5MovedLineText.ContainsKey(name) && l == 2))
                {
                    data.Add(name, $"ontype {l}");
                    // C1/C2's `$):` line (0-based 2) is the dropped statement's continuation: Format Selection aligns it
                    // to its block's level as it aligns the token-bearing twin's `z, $):` — not a refused cell
                    // (P22hVI_TheDroppedContinuationLine_IsAlignedLikeItsTokenBearingTwin).
                    if (!(name.StartsWith("C1", StringComparison.Ordinal) || name.StartsWith("C2", StringComparison.Ordinal)) || l != 2)
                        data.Add(name, $"range-line {l}");
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Every cell of the verifier's documents applies nothing, while the text the route would apply without its refusal
    /// is an edit the check refuses. 1e is R-FU's accepted loss (a bracket opened on an SPY0013-dropped line and closed
    /// later in the text still freezes the rest of the file: base repaired <c>          y = 2</c> on on-type 5 and
    /// range-line 5, and <c>      foo(1,</c> on on-type 3 / range-line 3) — recorded, posted on #2279, not cured.
    /// </summary>
    [Theory]
    [MemberData(nameof(R2NoEditCells))]
    public void P22hR2_TheVerifiersCells_ApplyNothing_AndTheCheckRefusesWhatTheyWouldApply(string name, string route)
    {
        var document = R2NoEditDocuments[name];
        RouteEdits(document, route).Should().BeEmpty("{0} {1}", name, route);
        var candidate = R5MovedLineText.GetValueOrDefault(name) ?? (route is "full" or "range-whole"
            ? R2BaseFullText.GetValueOrDefault(name) ?? FormattingFallback.ReindentDocument(document)
            : RangeCandidate(document, int.Parse(route.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture)));
        candidate.Should().NotBe(document, "{0} {1}: the candidate is an edit", name, route);
        if (RefusedAsAContinuationLine(document, route))
            return; // C1/C2's `$):` line: the handler's rule, the check aligns a continuation
        if (IndentationService.BuildIndentMap(document).HasOtherWhitespace)
        {
            // Lead ruling L12: the refusal is the funnel's, for the whole document, before any per-line check.
            var lines = Compiler.Formatting.LineDiff.Split(document).Lines;
            var whole = new TextEdit { Range = new LspRange(new Position(0, 0), new Position(lines.Count - 1, lines[^1].Length)), NewText = candidate };
            FormattingEdits.CheckedIndentOnly(document, new[] { whole }).Should().BeEmpty("{0} {1}: the funnel refuses a document indented with other whitespace", name, route);
            return;
        }

        FormattingFallback.IndentOnlyPreserved(document, candidate).Should().BeFalse("{0} {1}: the check refuses the candidate", name, route);
    }

    /// <summary>
    /// The map's facts behind the cells (lead rulings L8–L10): 1a's dropped header and its putative body are unplaced;
    /// in 1a-for both body lines are; 1d's <c>print(x)</c> starts a logical line (L10: the dropped line does not
    /// continue — the field's paren is the field's) and is judged; 1c's opener is frozen with its tail.
    /// </summary>
    /// <summary>
    /// The map's L11 facts: L6-SIB's putative body runs from the recovery line to the line before <c>w = 3</c>; N-NEST's
    /// body keeps the outer header's bound, so the dedented <c>z = 2</c> is in it. L12: a document with a line indented
    /// with other whitespace is marked for the whole (<see cref="IndentMap.HasOtherWhitespace"/>,
    /// <see cref="IndentationService.HasOtherLeadingWhitespace"/>).
    /// </summary>
    [Fact]
    public void P22hR5_TheMapReadsThePutativeBodyToItsShallowestBound_AndOtherWhitespace()
    {
        IndentationService.BuildIndentMap(R2NoEditDocuments["L6-SIB"]).UnplacedLines.Should().BeEquivalentTo(new[] { 2, 3, 4 });
        IndentationService.BuildIndentMap(R2NoEditDocuments["N-NEST"]).UnplacedLines.Should().BeEquivalentTo(new[] { 2, 3, 4, 5 });
        IndentationService.BuildIndentMap(R2NoEditDocuments["FF"]).HasOtherWhitespace.Should().BeTrue();
        IndentationService.BuildIndentMap(R2NoEditDocuments["NBSP far from the repair (L12 cost)"]).HasOtherWhitespace.Should().BeTrue();
        IndentationService.BuildIndentMap(A6).HasOtherWhitespace.Should().BeFalse();
        IndentationService.HasOtherLeadingWhitespace("\f  y").Should().BeTrue();
        IndentationService.HasOtherLeadingWhitespace("  \u00a0y").Should().BeTrue();
        IndentationService.HasOtherLeadingWhitespace(" \t y").Should().BeFalse();
    }

    /// <summary>
    /// The map's fact behind C1 (/verify-implementation of plan-f92797): the dropped physical line of a partially read
    /// logical line is that line's continuation even when the lexer read no token on it, so the recovery line below finds
    /// the dropped line by the logical line's START and its body is unplaced — the same facts as the token-bearing twin
    /// (<c>z, $):</c>, which 040c79ec2 already refused). Controls: a token-less line dropped WHOLE (SPY0013) after a
    /// completed logical line is still a logical start of its own; the dropped continuation on the source's last line,
    /// with no break after it, is still the continuation.
    /// </summary>
    /// <summary>
    /// The routes treat C1's token-less dropped continuation exactly as the token-bearing twin's (<c>z, $):</c>, refused
    /// at 040c79ec2 already): full refuses the whole candidate (the body below is unplaced, clause 6c); on-type refuses
    /// the <c>$):</c> line (not a logical start); Format Selection on that line aligns it to its block's level, as it
    /// aligns any continuation — no statement changes block. The parity is the point: the body is held because the
    /// dropped line is read as the statement's continuation, not because of what the lexer happened to read on it.
    /// </summary>
    [Fact]
    public void P22hVI_TheDroppedContinuationLine_IsAlignedLikeItsTokenBearingTwin()
    {
        var c1 = R2NoEditDocuments["C1 for header, colon on the dropped continuation at column 0"];
        const string twin = "def main():\n    for i in range(1,\nz, $):\n        print(i)\n    print(0)\n";
        foreach (var (name, document, aligned) in new[] { ("C1", c1, "    $):"), ("twin", twin, "    z, $):") })
        {
            RouteEdits(document, "full").Should().BeEmpty("{0} full: the body below the header is unplaced", name);
            RouteEdits(document, "range-whole").Should().BeEmpty("{0} range-whole", name);
            RouteEdits(document, "ontype 2").Should().BeEmpty("{0} on-type 2: not a logical start", name);
            RouteEdits(document, "ontype 3").Should().BeEmpty("{0} on-type 3: unplaced", name);
            RouteEdits(document, "range-line 3").Should().BeEmpty("{0} range-line 3: unplaced", name);
            RouteCell(document, "range-line 2").Applied.Should().Be(WithLine(document, 2, aligned), "{0} range-line 2: a continuation aligned to its block's level", name);
        }
    }

    [Fact]
    public void P22hVI_ADroppedContinuationLineWithNoToken_IsItsLogicalLinesContinuation()
    {
        var c1 = IndentationService.BuildIndentMap(R2NoEditDocuments["C1 for header, colon on the dropped continuation at column 0"]);
        c1.LogicalLineStarts.Should().BeEquivalentTo(new[] { 1, 2, 4, 5 }, "`$):` continues `for i in range(1,`");
        c1.RecoveryLines.Should().ContainKey(4).WhoseValue.Should().Be(new RecoveryLine(2, 4, PossibleHeader: true));
        c1.UnplacedLines.Should().BeEquivalentTo(new[] { 2, 4 });
        c1.LineIndent[3].Should().Be(1, "a continuation line takes its block's level");

        var twin = IndentationService.BuildIndentMap("def main():\n    for i in range(1,\nz, $):\n        print(i)\n    print(0)\n");
        twin.LogicalLineStarts.Should().BeEquivalentTo(c1.LogicalLineStarts);
        twin.RecoveryLines.Should().BeEquivalentTo(c1.RecoveryLines);
        twin.UnplacedLines.Should().BeEquivalentTo(c1.UnplacedLines);

        var c2 = IndentationService.BuildIndentMap(R2NoEditDocuments["C2 if header, colon on the dropped backslash continuation"]);
        c2.LogicalLineStarts.Should().BeEquivalentTo(new[] { 1, 2, 4, 5 });
        c2.UnplacedLines.Should().BeEquivalentTo(new[] { 2, 4 });

        IndentationService.BuildIndentMap("def main():\n    x = 1\n      y = 1\n").LogicalLineStarts.Should().Contain(3, "a line dropped whole starts a logical line");
        IndentationService.BuildIndentMap("def main():\n    for i in range(1,\n$):").LogicalLineStarts.Should().BeEquivalentTo(new[] { 1, 2 },
            "the dropped continuation on the last line, with no break after it");
    }

    [Fact]
    public void P22hR2_TheMapReadsThePossibleHeader_ThePutativeBody_TheJudgedLine_AndTheFrozenOpener()
    {
        IndentationService.BuildIndentMap(R2NoEditDocuments["1a for"]).UnplacedLines.Should().BeEquivalentTo(new[] { 2, 3, 4 });
        IndentationService.BuildIndentMap(R2NoEditDocuments["1a wide"]).UnplacedLines.Should().BeEquivalentTo(new[] { 2, 3 });
        IndentationService.BuildIndentMap(R2NoEditDocuments["1b"]).UnplacedLines.Should().BeEquivalentTo(new[] { 2, 3 });
        IndentationService.BuildIndentMap(R2NoEditDocuments["1b no header"]).RecoveryLines.Should().ContainKey(3)
            .WhoseValue.Should().Be(new RecoveryLine(2, 4, PossibleHeader: false));
        IndentationService.BuildIndentMap(R2NoEditDocuments["1b no header"]).UnplacedLines.Should().BeEquivalentTo(new[] { 2, 3 },
            "two tabs are 16 columns, a level and more deeper than the dropped line (L6, measured in columns)");
        IndentationService.IndentColumns("\t\ty").Should().Be(16);
        IndentationService.IndentColumns("    \ty").Should().Be(8);

        var d = IndentationService.BuildIndentMap(Verifier1d);
        d.LogicalLineStarts.Should().Contain(4, "print(x) starts a logical line");
        d.OpenBracketLine.Should().BeNull();

        IndentationService.BuildIndentMap(Verifier1c).FrozenFrom.Should().Be(3);
        IndentationService.BuildIndentMap("def main():\n        x = 1\n        y = (\n").FrozenFrom.Should().Be(4, "an opener on the last line freezes nothing");
    }

    // ---- P22i: a comment-only line is left verbatim by every indent-only route (R-GD, #2290) ----
    // Each document is named by its place in the matrix (plan-ee5544 Current State); 0-based lines. Every document but the
    // parseable control does not parse, so every route below is an indent-only pass.

    /// <summary>C1: an unterminated string above main, an indented comment in main's body (#2290's first document).</summary>
    internal const string P22iC1 = "def f():\n    y = \"abc\n\ndef main():\n    # c1\n    b = 1\n";

    /// <summary>C2: C1 with <c>$</c> for the string.</summary>
    private const string P22iC2 = "def f():\n    y = $\n\ndef main():\n    # c1\n    b = 1\n";

    /// <summary>C3: the misindent control — <c>f</c>'s body at 6 spaces (SPY0013), the comment in main's body.</summary>
    private const string P22iC3 = "def f():\n      y = 1\n\ndef main():\n    # c1\n    b = 1\n";

    /// <summary>C5: a comment deeper than its block.</summary>
    internal const string P22iC5 = "def f():\n    y = $\n\ndef main():\n        # deep\n    b = 1\n";

    /// <summary>C6: a column-0 comment inside main's body (a control: level 0 either way).</summary>
    private const string P22iC6 = "def f():\n    y = $\n\ndef main():\n# c0\n    b = 1\n";

    /// <summary>C7: a comment as the last line of a body, before a dedent.</summary>
    private const string P22iC7 = "def f():\n    y = $\n\ndef main():\n    b = 1\n    # end\n\ndef g():\n    pass\n";

    /// <summary>C8: a module-level comment between definitions (a control).</summary>
    private const string P22iC8 = "def f():\n    y = $\n\n# between\ndef main():\n    b = 1\n";

    /// <summary>C9: a comment above the first statement (a control).</summary>
    private const string P22iC9 = "# top\ndef f():\n    y = $\n\ndef main():\n    b = 1\n";

    /// <summary>C10: a comment inside a CLOSED bracket at the continuation's width.</summary>
    private const string P22iC10 = "def f():\n    y = $\n\ndef main():\n    xs = [1,\n          # inner\n          2]\n    b = 1\n";

    /// <summary>C11: a 2-space body with a comment.</summary>
    private const string P22iC11 = "def main():\n  # c\n  x = 1\n  print(x)\n";

    /// <summary>C12: an 8-space S3 cut with a comment.</summary>
    private const string P22iC12 = "def main():\n        # c\n        x = 1\n        if x:\n";

    /// <summary>C13: C1 with a backtick name for the string.</summary>
    private const string P22iC13 = "def f():\n    y = `ab\n\ndef main():\n    # c1\n    b = 1\n";

    /// <summary>C14: C1 with an unterminated f-string replacement field.</summary>
    private const string P22iC14 = "def f():\n    y = f\"{x\n\ndef main():\n    # c1\n    b = 1\n";

    /// <summary>C15: a tab-indented comment.</summary>
    private const string P22iC15 = "def f():\n    y = $\n\ndef main():\n\t# tab\n    b = 1\n";

    /// <summary>C16: a comment inside the putative body below a dropped header (L8) — in neither LineIndent nor UnplacedLines.</summary>
    private const string P22iC16 = "def main():\n    if foo($):\n        # c\n        y = 1\n";

    /// <summary>C16b: C16 with the body at 10 spaces (deeper than a level).</summary>
    private const string P22iC16b = "def main():\n    if foo($):\n          # c\n          y = 1\n";

    /// <summary>C17: a comment on the frozen tail below an open bracket (a control: clause 7 and the builders' frozen check).</summary>
    private const string P22iC17 = "def main():\n    xs = [1,\n        # c\n        2\n";

    /// <summary>The prober's axis (a'): a comment directly after a dropped line, before its recovery line.</summary>
    private const string P22iAfterDropped = "def main():\n    y = $\n    # c\n    z = 1\n";

    /// <summary>The prober's axis (b'): an indented comment-only LAST line with no line break after it.</summary>
    private const string P22iLastNoBreak = "def f():\n    y = \"abc\n\ndef main():\n    b = 1\n    # last";

    /// <summary>
    /// The P22i cells that apply no edit, each with the text the route applied @ 53ee2d993 (the comment written at column 0:
    /// the direction "wrong edit → no edit"), or — where the route applied nothing then either (C16 full / range-whole,
    /// refused by clause 6c because the candidate also moves <c>y</c>; C17, frozen) — the candidate the check refuses.
    /// On-type rows are the handler's own rule (a comment line is never a logical start): no edit before and after.
    /// </summary>
    public static TheoryData<string, string, string, string> P22iNoEditCells
    {
        get
        {
            var cells = new TheoryData<string, string, string, string>();
            foreach (var (name, document, line, atZero) in new[]
            {
                ("C1", P22iC1, 4, "# c1"), ("C2", P22iC2, 4, "# c1"), ("C13", P22iC13, 4, "# c1"), ("C14", P22iC14, 4, "# c1"),
                ("C5", P22iC5, 4, "# deep"), ("C7", P22iC7, 5, "# end"), ("C15", P22iC15, 4, "# tab"),
                ("a'", P22iAfterDropped, 2, "# c"), ("b'", P22iLastNoBreak, 5, "# last"),
            })
            {
                var candidate = WithLine(document, line, atZero);
                cells.Add(name, document, "full", candidate);
                cells.Add(name, document, "range-whole", candidate);
                cells.Add(name, document, $"range-line {line}", candidate);
                cells.Add(name, document, $"ontype {line}", candidate);
            }

            // C3's comment line alone (its full / range-whole rows are repairs).
            cells.Add("C3", P22iC3, "range-line 4", WithLine(P22iC3, 4, "# c1"));
            cells.Add("C3", P22iC3, "ontype 4", WithLine(P22iC3, 4, "# c1"));
            // C16 / C16b range-line 2: @ 53ee2d993 the comment went to column 0 — clause 8 is the one clause that refuses it.
            cells.Add("C16", P22iC16, "range-line 2", WithLine(P22iC16, 2, "# c"));
            cells.Add("C16b", P22iC16b, "range-line 2", WithLine(P22iC16b, 2, "# c"));
            cells.Add("C16", P22iC16, "ontype 2", WithLine(P22iC16, 2, "# c"));
            // C16 full / range-whole: no edit before and after — the builder's candidate moves `y` (6c).
            cells.Add("C16", P22iC16, "full", WithLine(P22iC16, 3, "    y = 1"));
            cells.Add("C16", P22iC16, "range-whole", WithLine(P22iC16, 3, "    y = 1"));
            foreach (var route in new[] { "full", "range-whole", "range-line 2", "ontype 2" })
                cells.Add("C17", P22iC17, route, WithLine(P22iC17, 2, "    # c"));
            return cells;
        }
    }

    [Theory]
    [MemberData(nameof(P22iNoEditCells))]
    public void P22i_TheRoutesLeaveTheCommentLine_AndTheCheckRefusesWhatTheyWouldApply(string name, string document, string route, string candidate)
    {
        RouteEdits(document, route).Should().BeEmpty("{0} {1}", name, route);
        candidate.Should().NotBe(document, "{0} {1}: the candidate is an edit", name, route);
        // On-type refuses a line that does not start a logical line itself — a comment line never does. Every other route
        // has no such rule, so the check's refusal is asserted for each of them, range-line included (clause 8).
        if (route.StartsWith("ontype ", StringComparison.Ordinal))
            return;
        FormattingFallback.IndentOnlyPreserved(document, candidate).Should().BeFalse("{0} {1}: the check refuses the candidate", name, route);
    }

    /// <summary>
    /// The P22i cells that apply a repair beside a kept comment — the positive controls that a route still repairs code
    /// (@ 53ee2d993 each also wrote the comment at column 0; a clause-only tree, without the builder skips, refuses each
    /// candidate whole and repairs nothing). C11 and C12 keep their comment at the old width while the code is re-indented
    /// — the consequence of R-GD (a), stated in the docs.
    /// </summary>
    public static TheoryData<string, string, string, string> P22iRepairCells => new()
    {
        { "C3", P22iC3, "full", WithLine(P22iC3, 1, "    y = 1") },
        { "C3", P22iC3, "range-whole", WithLine(P22iC3, 1, "    y = 1") },
        { "C10", P22iC10, "full", WithLine(P22iC10, 6, "    2]") },
        { "C10", P22iC10, "range-whole", WithLine(P22iC10, 6, "    2]") },
        { "C11", P22iC11, "full", "def main():\n  # c\n    x = 1\n    print(x)\n" },
        { "C11", P22iC11, "range-whole", "def main():\n  # c\n    x = 1\n    print(x)\n" },
        { "C12", P22iC12, "full", "def main():\n        # c\n    x = 1\n    if x:\n" },
        { "C12", P22iC12, "range-whole", "def main():\n        # c\n    x = 1\n    if x:\n" },
    };

    [Theory]
    [MemberData(nameof(P22iRepairCells))]
    public void P22i_TheRoutesRepairTheCode_AndKeepTheComment(string name, string document, string route, string repaired)
    {
        RouteCell(document, route).Applied.Should().Be(repaired, "{0} {1}", name, route);
    }

    /// <summary>
    /// The controls: a column-0 comment in a body (C6), a module-level comment (C8, C9) — level 0 either way — and the
    /// parseable twin of C1 (C4: the real formatter places the comment with its code, guarantee 2 — kept at 4).
    /// </summary>
    [Fact]
    public void P22i_Controls_NoEditOnAColumn0Comment_AndTheParseableTwinKeepsItsComment()
    {
        foreach (var (document, line) in new[] { (P22iC6, 4), (P22iC8, 3), (P22iC9, 0) })
        {
            foreach (var route in new[] { "full", "range-whole", $"range-line {line}", $"ontype {line}" })
                RouteEdits(document, route).Should().BeEmpty(route);
        }

        const string c4 = "def f():\n    y = 1\n\ndef main():\n    # c1\n    b = 1\n";
        RouteCell(c4, "full").Applied.Should().Contain("\n    # c1\n", "the parseable document is formatted by the real formatter (guarantee 2)");
        RouteEdits(c4, "range-line 4").Should().BeEmpty();
    }

    /// <summary>
    /// Both builders return a comment-only line verbatim BEFORE the check — the unchecked candidate keeps every comment
    /// line byte-identical (a builder that forgets is refused by clause 8, which then withholds the code repair beside it).
    /// </summary>
    [Fact]
    public void P22i_BothBuilders_ReturnACommentLineVerbatim()
    {
        foreach (var document in new[] { P22iC1, P22iC3, P22iC5, P22iC7, P22iC10, P22iC11, P22iC12, P22iC15, P22iC16, P22iLastNoBreak })
        {
            var map = IndentationService.BuildIndentMap(document);
            map.CommentLines.Should().NotBeEmpty(document);
            var (source, _) = Compiler.Formatting.LineDiff.Split(document);
            var full = Compiler.Formatting.LineDiff.Split(FormattingFallback.ReindentDocument(document)).Lines;
            var range = Compiler.Formatting.LineDiff.Split(LspFormattingDriver.ApplyStrict(document,
                SharpyRangeFormattingHandler.ComputeIndentOnlyRangeEdits(document, 0, source.Count - 1))).Lines;
            foreach (var line in map.CommentLines)
            {
                full[line - 1].Should().Be(source[line - 1], "the full fallback's builder, line {0} of {1}", line, document);
                range[line - 1].Should().Be(source[line - 1], "the range fallback's builder, line {0} of {1}", line, document);
            }
        }
    }

    /// <summary>
    /// Clause 8 at the check seam: C3 with its comment at column 0 is refused, the code repair beside the kept comment is
    /// accepted; C16 with its comment at column 0 and <c>y</c> unchanged is refused — the one cell no other clause refuses
    /// (the comment is in neither <c>LineIndent</c> nor <c>UnplacedLines</c>); a <c>#</c> line inside a closed string is
    /// string content (clause 3), never a comment line.
    /// </summary>
    [Fact]
    public void Comment_ADedentedCommentLine_IsRefused_TheCodeRepairBesideItIsNot()
    {
        var repaired = WithLine(P22iC3, 1, "    y = 1");
        FormattingFallback.IndentOnlyPreserved(P22iC3, WithLine(repaired, 4, "# c1")).Should().BeFalse();
        FormattingFallback.IndentOnlyPreserved(P22iC3, repaired).Should().BeTrue();
        FormattingFallback.IndentOnlyPreserved(P22iC16, WithLine(P22iC16, 2, "# c")).Should().BeFalse();
        FormattingFallback.IndentOnlyPreserved(P22iC5, WithLine(P22iC5, 4, "# deep")).Should().BeFalse();

        const string literal = "def main():\n    s = \"\"\"\n        # key\n    \"\"\"\n        if s:\n";
        var map = IndentationService.BuildIndentMap(literal);
        map.LiteralLines.Should().Contain(3);
        map.CommentLines.Should().NotContain(3, "a # line inside a string is string content");
        FormattingFallback.IndentOnlyPreserved(literal, WithLine(literal, 2, "    # key")).Should().BeFalse("clause 3");
    }

    [Fact]
    public void P22i_TheMapNamesItsCommentLines()
    {
        IndentationService.BuildIndentMap(P22iC10).CommentLines.Should().BeEquivalentTo(new[] { 6 });
        IndentationService.BuildIndentMap(P22iC15).CommentLines.Should().BeEquivalentTo(new[] { 5 });
        IndentationService.BuildIndentMap(P22iC17).CommentLines.Should().BeEquivalentTo(new[] { 3 });
        var c16 = IndentationService.BuildIndentMap(P22iC16);
        c16.CommentLines.Should().BeEquivalentTo(new[] { 3 });
        c16.UnplacedLines.Should().Contain(4).And.NotContain(3, "the width loop skips a comment line before 6c's set is built");
        c16.LineIndent.Should().NotContainKey(3);
        IndentationService.IsCommentOnlyLine(" \t # x").Should().BeTrue();
        IndentationService.IsCommentOnlyLine("x = 1  # x").Should().BeFalse();
        IndentationService.IsCommentOnlyLine("   ").Should().BeFalse();
    }
}
