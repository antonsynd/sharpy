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
    /// unterminated f-string whose replacement field is closed — it ends at its line, as its plain-string
    /// twin does — so the line routes keep the 4-space repair they applied @ 3c70ea492 (refused @ 4b7428567).
    /// </summary>
    [Fact]
    public void DirectionControl_UnterminatedFStringWithNoOpenField_LineRoutesKeepTheirRepair()
    {
        const string document = "def main():\n  x: int = 1\n  print(f\"total: {x}\n  print(x)\n";

        _driver.Range(document, LineRange(document, 1)).Applied.Should().Be(WithLine(document, 1, "    x: int = 1"));
        _driver.OnType(document, 1).Applied.Should().Be(WithLine(document, 1, "    x: int = 1"));
        _driver.Range(document, LineRange(document, 3)).Applied.Should().Be(WithLine(document, 3, "    print(x)"));
    }

    // ---- P22g (found by the route-parity sweep's S6x direction control): error recovery emits no Newline, so the
    // line after an error read as a continuation of the dropped one. The lexer now records where recovery resumed
    // (Lexer.RecoveryResumes); LiteralSpans resets there, and the indent-only check judges the hidden line's depth.

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
    /// drops CONTINUES — a bracket left open, a trailing backslash — the next line is the user's continuation line, and
    /// the fallback aligns it to its block as it does every continuation. Judging its depth (HiddenAfterRecovery) refused
    /// the whole repair; @ 926670989 these were repaired to exactly this text. The dropped line's continuation is now
    /// recorded by the lexer (RecoveryResumesAfterAContinuedLine) and such a line is not depth-judged.
    /// </summary>
    public static TheoryData<string, string> ContinuedLineDroppedByRecovery => new()
    {
        { "call $", "    x = foo($,\n        a)\n    print(x)\n" },
        { "call string", "    x = foo(\"abc,\n        a)\n    print(x)\n" },
        { "list $", "    xs = [1, 2, $\n        3]\n    print(xs)\n" },
        { "backslash", "    y = 1 + $ \\\n        2\n    print(y)\n" },
    };

    [Theory]
    [MemberData(nameof(ContinuedLineDroppedByRecovery))]
    public void DirectionControl_AContinuedLineDroppedByRecovery_FullAndRangeWholeKeepTheirRepair(string name, string body)
    {
        var document = "def f():\n  return 1\n\ndef main():\n" + body;
        var lines = document.Split('\n');
        var repaired = WithLine(WithLine(document, 1, "    return 1"), 5, "    " + lines[5].TrimStart());
        _driver.Full(document).Applied.Should().Be(repaired, name);
        _driver.Range(document, Lines(0, lines.Length - 1)).Applied.Should().Be(repaired, name);
    }

    /// <summary>
    /// An 8-space document whose <c>dr"""</c> closer line aborts (<c>""" + $</c>): the line after it is hidden from
    /// the logical lines. @ 926670989 on-type and range-line 1 re-indented line 1 alone to 4 spaces and left
    /// <c>print(s)</c> at 8 — one block deeper than its sibling. Full re-indents both and keeps its repair.
    /// </summary>
    internal const string WideCloserLineAbort =
        "def main():\n        s: str = dr\"\"\"\n        \\d+\n        \\s+\n        \"\"\" + $\n        print(s)\n";

    [Fact]
    public void ALineHiddenAfterRecovery_KeepsItsBlock_PartialReindentsAreRefused()
    {
        const string d = WideCloserLineAbort;
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
    /// f-string at the spec's quote and never saw the opener). Now the lexer records <c>DroppedOpener</c> and the
    /// routes apply nothing; the unchecked candidate shows what they would have done.
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
}
