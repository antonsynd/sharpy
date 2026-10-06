using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Formatting;
using Xunit;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.Formatting;

public class FormatterServiceTests
{
    [Fact]
    public void Format_SimpleAssignment_ProducesCorrectOutput()
    {
        var result = FormatterService.Format("x = 1\n");
        result.FormattedText.Should().Contain("x = 1");
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Format_AlreadyFormatted_HasChangesIsFalse()
    {
        var source = "x = 1\n";
        var result = FormatterService.Format(source);
        result.HasChanges.Should().BeFalse();
        result.FormattedText.Should().Be(source);
    }

    [Fact]
    public void Format_PreservesComments()
    {
        var source = "# header\nx = 1  # inline\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("# header");
        result.FormattedText.Should().Contain("# inline");
    }

    [Fact]
    public void Format_InsertsBlankLinesBetweenTopLevelDefs()
    {
        var source = "def foo():\n    pass\ndef bar():\n    pass\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("pass\n\n\ndef bar():");
        result.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void Format_SyntaxErrors_ReturnsOriginalSource()
    {
        var source = "def foo(\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Be(source);
        result.HasChanges.Should().BeFalse();
        result.Diagnostics.Should().NotBeEmpty();
    }

    // Sharpy indentation is exactly 4 spaces per level, no tabs (indentation.md). Until 2026-09-30
    // FormatOptions.IndentSize/UseTabs wrote 2-space or tab indentation — a file that does not lex
    // (SPY0013/SPY0012); owner ruling 2026-09-30 (P22b): the formatter always writes 4 spaces and has
    // no indentation option.
    [Fact]
    public void Format_AlwaysWritesFourSpaceIndentation()
    {
        var source = "def foo():\n        if True:\n                pass\n";
        var result = FormatterService.Format(source);
        result.Diagnostics.Should().BeEmpty();
        result.FormattedText.Should().Be("def foo():\n    if True:\n        pass\n");
    }

    [Fact]
    public void FormatOptions_HasNoIndentationKnob()
    {
        // A re-added IndentSize/UseTabs would be a knob whose only effect is an unlexable file.
        typeof(FormatOptions).GetProperty("IndentSize").Should().BeNull();
        typeof(FormatOptions).GetProperty("UseTabs").Should().BeNull();
        typeof(FormatOptions).GetProperty("LineEnding").Should().NotBeNull("positive control: the reflection sees the record's properties");
    }

    [Fact]
    public void Format_Idempotent()
    {
        var source = "def foo():\n    pass\ndef bar():\n    pass\n";
        var first = FormatterService.Format(source);
        var second = FormatterService.Format(first.FormattedText);
        second.FormattedText.Should().Be(first.FormattedText);
        second.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void Format_EmptyInput()
    {
        var result = FormatterService.Format("");
        result.FormattedText.Should().BeEmpty();
        result.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void Format_MultiLineString_ContentPreserved()
    {
        var source = "x = \"\"\"hello\n  world\"\"\"\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("hello\n  world");
    }

    [Fact]
    public void Format_StripsTrailingWhitespace()
    {
        var source = "x = 1   \ny = 2  \n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Be("x = 1\ny = 2\n");
    }

    [Fact]
    public void Format_StripsTrailingWhitespace_OnBlankLines()
    {
        var source = "x = 1\n   \ny = 2\n";
        var result = FormatterService.Format(source);
        var lines = result.FormattedText.Split('\n');
        lines.Should().AllSatisfy(line => line.Should().Be(line.TrimEnd(' ', '\t')));
    }

    [Fact]
    public void Format_StripsTrailingTabs()
    {
        var source = "x = 1\t\t\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Be("x = 1\n");
    }

    [Fact]
    public void StripTrailingWhitespace_PreservesTrailingNewline()
    {
        var result = FormatterService.StripTrailingWhitespace("x = 1  \ny = 2  ", FormatOptions.Default);
        result.Should().EndWith("\n");
    }

    [Fact]
    public void StripTrailingWhitespace_NoTrailingNewline_WhenDisabled()
    {
        var options = new FormatOptions { TrailingNewline = false };
        var result = FormatterService.StripTrailingWhitespace("x = 1  ", options);
        result.Should().Be("x = 1");
    }

    [Fact]
    public void Format_MultipleDecorators_BeforeFunction()
    {
        var source = "def foo():\n    pass\n@staticmethod\n@abstractmethod\ndef bar():\n    pass\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("pass\n\n\n@staticmethod\n@abstractmethod\ndef bar():");
    }

    [Fact]
    public void Format_NestedClassWithMethods()
    {
        var source = "class Outer:\n    class Inner:\n        def a(self):\n            pass\n        def b(self):\n            pass\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("pass\n\n        def b(self):");
    }

    [Fact]
    public void Format_ImportThenAssignmentThenFunction()
    {
        var source = "import os\nx = 1\ndef foo():\n    pass\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("x = 1\n\n\ndef foo():");
    }

    [Fact]
    public void Format_FunctionWithPassOnly()
    {
        var source = "def foo():\n    pass\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("def foo():\n    pass\n");
    }

    [Fact]
    public void Format_BodyCommentPreserved()
    {
        var source = "def foo():\n    # body comment\n    x = 1\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("# body comment\n    x = 1");
    }

    [Fact]
    public void Format_NestedTypeDefinitions_GetBlankLines()
    {
        var source = "class Outer:\n    class Inner1:\n        pass\n    class Inner2:\n        pass\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("pass\n\n    class Inner2:");
    }

    // -----------------------------------------------------------------------
    // The meaning-preservation net (P22b Phase 2, SPY0912): Format re-reads its own output and
    // refuses it — source returned unchanged — when it would not re-parse, change the AST, drop a
    // comment or drop a backtick escape. Each Net_* cell hand-damages ONE invariant; the positive
    // controls show an undamaged (source, Format(source)) pair passes.
    // -----------------------------------------------------------------------

    private static Sharpy.Compiler.Parser.Ast.Module ParseWithTrivia(string source)
    {
        var tokens = new SLexer(source, preserveTrivia: true).TokenizeAll();
        var parser = new SParser(tokens);
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse("the net's source must parse");
        return module;
    }

    private static CompilerDiagnostic? Net(string source, string output) =>
        FormatterService.CheckMeaningPreserved(source, ParseWithTrivia(source), output);

    private static void AssertDeclined(CompilerDiagnostic? diagnostic, string what)
    {
        diagnostic.Should().NotBeNull();
        diagnostic!.Code.Should().Be(DiagnosticCodes.Infrastructure.FormatterDeclined);
        diagnostic.IsError.Should().BeTrue();
        diagnostic.Message.Should().StartWith("formatting declined: the output would ");
        diagnostic.Message.Should().Contain(what);
        diagnostic.Message.Should().EndWith("; the file was left unchanged");
    }

    [Fact]
    public void Net_RefusesAnOutputThatDoesNotReparse()
    {
        var source = "def main():\n    x = 1\n    print(x)\n";
        var output = "def main():\n    x = = 1\n    print(x)\n";

        AssertDeclined(Net(source, output), "would not re-parse (first error at formatted line 2");
    }

    [Fact]
    public void Net_RefusesAnOutputThatChangesTheStructure()
    {
        // Same comments, same escapes, parses — only the AST differs.
        var source = "x = 1\ny = 2  # two\n";
        var output = "x = 1\ny = 3  # two\n";

        var diagnostic = Net(source, output);

        AssertDeclined(diagnostic, "would change the program's structure (first differing statement at line 2)");
        diagnostic!.Line.Should().Be(2);
    }

    [Fact]
    public void Net_RefusesAnOutputMissingAComment()
    {
        // #2077's live cell: the bracket comment is gone and nothing else changed — the structural
        // comparer ignores trivia, so only the comment-sequence check sees it.
        var source = "def main():\n    xs = [1,  # inner\n        2]\n    print(xs)\n";
        var output = "def main():\n    xs = [1, 2]\n    print(xs)\n";

        var diagnostic = Net(source, output);

        AssertDeclined(diagnostic, "would drop comment '# inner' at line 2");
        diagnostic!.Line.Should().Be(2);
    }

    [Fact]
    public void Net_RefusesAnOutputAddingOrReorderingAComment()
    {
        AssertDeclined(Net("x = 1  # a\n", "x = 1  # a\n# b\n"), "would add comment '# b'");
        AssertDeclined(Net("# a\n# b\nx = 1\n", "# b\n# a\nx = 1\n"), "would move comment '# a' at line 1 out of source order");
    }

    [Fact]
    public void Net_RefusesAnOutputDroppingAnEscape()
    {
        // #2157's cell on a NON-keyword name, so the damaged output still parses and (with no escape
        // flag on KeywordArgument at 4ef844961) is structurally equal — only the token-based escape
        // check sees it. The message is asserted, so a structural refusal cannot stand in for it.
        var source = "def f(`x`: int) -> int:\n    return `x`\n\n\ndef main():\n    print(f(`x`=7))\n";
        var output = "def f(`x`: int) -> int:\n    return `x`\n\n\ndef main():\n    print(f(x=7))\n";

        var diagnostic = Net(source, output);

        AssertDeclined(diagnostic, "would drop the backtick escape on 'x' at line 6");
        diagnostic!.Line.Should().Be(6);
    }

    /// <summary>
    /// A comment that keeps its place in the sequence but changes its attachment has MOVED (#2077):
    /// the two shapes the unparser produced at 4ef844961 — an <c>else</c> header comment written on
    /// the <c>if</c> line, and a block-end comment hoisted out of the block it ends.
    /// </summary>
    [Fact]
    public void Net_RefusesAnOutputThatMovesAComment()
    {
        AssertDeclined(
            Net("def main():\n    x = 0\n    if x > 0:\n        print(1)\n    else:  # c\n        print(2)\n",
                "def main():\n    x = 0\n    if x > 0:  # c\n        print(1)\n    else:\n        print(2)\n"),
            "would move comment '# c' at line 5");
        AssertDeclined(
            Net("def main():\n    if True:\n        print(1)\n        # end of if\n    print(2)\n",
                "def main():\n    if True:\n        print(1)\n    # end of if\n    print(2)\n"),
            "would move comment '# end of if' at line 4 (from block depth 2 to 1)");
        AssertDeclined(
            Net("@d\ndef f():  # c\n    pass\n", "@d  # c\ndef f():\n    pass\n"),
            "would move comment '# c' at line 2");
    }

    /// <summary>
    /// The exemption, positively: layout is not attachment. A body re-indented from 8 columns to 4
    /// — its block-end comment and a mid-body comment written at an odd column (re-indented to the
    /// statement it precedes) — moves no comment.
    /// </summary>
    [Fact]
    public void Net_AcceptsAReindentThatKeepsEveryAttachment()
    {
        var source = "def main():\n        if True:\n                print(1)\n                # end of if\n# odd column, mid-body\n        print(2)\n";
        var output = "def main():\n    if True:\n        print(1)\n        # end of if\n    # odd column, mid-body\n    print(2)\n";

        Net(source, output).Should().BeNull();
    }

    [Fact]
    public void Net_ComparesCommentTextWithoutTheTrailingWhitespaceTheFormatterStrips()
    {
        Net("x = 1  # c   \n", "x = 1  # c\n").Should().BeNull();
    }

    [Theory]
    [InlineData("def foo():\n    pass\ndef bar():  # c\n    return 1\n")]
    [InlineData("# header\nx   =   1  # inline\ndef f(`class`: int) -> int:\n        return `class`\n")]
    [InlineData("def main():\n        s = \"\"\"p  \nq\"\"\"  # note\n        print(s)\n")]
    public void Net_PositiveControl_AnUndamagedFormatPasses(string source)
    {
        var result = FormatterService.Format(source);

        result.Diagnostics.Should().BeEmpty();
        result.HasChanges.Should().BeTrue("the control must exercise a real rewrite, not the identity");
        Net(source, result.FormattedText).Should().BeNull();
    }

    [Fact]
    public void Format_KeepsABracketComment_TheFormerRefusalCellIsWrittenVerbatim()
    {
        // #2077 live cell end to end: at 4ef844961 Format returned `xs = [1, 2]` (comment gone); the
        // P22b Phase 2 net declined the drop; the Phase 4 Task 2 cursor kept the comment but wrote it
        // above the statement, which the net declined as a move. Since Task 3 the statement is written
        // verbatim from source, re-indented with its first line (8 → 4, so the continuation 12 → 8).
        var source = "def main():\n        xs = [1,  # inner\n            2]\n        print(xs)\n";

        var result = FormatterService.Format(source, filePath: "p2077.spy");

        result.Diagnostics.Should().BeEmpty();
        result.FormattedText.Should().Be("def main():\n    xs = [1,  # inner\n        2]\n    print(xs)\n");
        result.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void Format_KeepsAKeywordArgumentEscape_TheFormerRefusalCellFormatsClean()
    {
        // #2157 live cell end to end. At 4ef844961 Format returned `f(class=7)`, which does not parse;
        // with the SPY0912 net (Phase 2) it declined with "would drop the backtick escape on 'class'
        // at line 6". Phase 3 (KeywordArgument.IsNameBacktickEscaped written through WriteName) flips
        // the cell: the escape is kept and Format reports nothing.
        var source = "def f(`class`: int) -> int:\n    return `class`\n\n\ndef main():\n    print(f(`class`=7))\n";

        var result = FormatterService.Format(source);

        result.Diagnostics.Should().BeEmpty();
        result.FormattedText.Should().Contain("print(f(`class`=7))");
        Net(source, result.FormattedText).Should().BeNull();
    }

    /// <summary>
    /// P22b verify-round R1: the comment-placement check over-refused a comment after a token the
    /// unparser legitimately re-spells — a type shorthand (<c>{int}</c> → <c>set[int]</c>, …) or a
    /// partial-application placeholder (<c>add(5, _)</c> → the lambda the parser lowered it to). At
    /// 99c3a5ae8 each cell was declined with "would move comment '# c'". Each must format, pass the
    /// net, and keep the comment on the line of the statement it trails (or own-line before the next).
    /// </summary>
    [Theory]
    [InlineData("def add(a: int, b: int) -> int:\n    return a + b\n\n\ndef main():\n    g = add(5, _)  # c\n    print(g(3))\n", "g = ")]
    [InlineData("def f(x: int, y: int) -> int:\n    return x * 10 + y\n\n\ndef main():\n    fix_x: (int) -> int = f(x=5, y=_)  # c\n    print(fix_x(7))\n", "fix_x: ")]
    [InlineData("def f(x: int) -> int:\n    return x\n\n\ndef main():\n    x = 5 |> f(_)  # c\n    print(x)\n", "x = ")]
    [InlineData("def main():\n    x: {int}  # c\n    x = {1}\n    print(x)\n", "x: ")]
    [InlineData("def main():\n    d: {str: int}  # c\n    t: (int, int)\n    print(1)\n", "d: ")]
    [InlineData("def main():\n    t: (int, int)  # c\n    print(1)\n", "t: ")]
    [InlineData("def main():\n    f: (int) -> int  # c\n    print(1)\n", "f: ")]
    [InlineData("def main():\n    counter: () -> int  # c\n    print(1)\n", "counter: ")]
    [InlineData("class C:\n    x: {int}  # c\n    y: int\n", "x: ")]
    [InlineData("def main():\n    x: {int}\n    # c\n    print(1)\n", "# c")]
    [InlineData("def f() -> ():  # c\n    return ()\n", "def f() -> ")]
    [InlineData("def main():\n    v: tuple[()] = ()  # c\n    print(len(v))\n", "v: ")]
    [InlineData("def main():  # c\n    print(_)\n", "def main():")]
    public void Format_KeepsACommentAfterARespelledToken_TheNetAccepts(string source, string commentLinePrefix)
    {
        var result = FormatterService.Format(source);

        result.Diagnostics.Should().BeEmpty();
        Net(source, result.FormattedText).Should().BeNull();
        var commentLine = result.FormattedText.Split('\n').Single(l => l.Contains("# c"));
        commentLine.TrimStart().Should().StartWith(commentLinePrefix);
        commentLine.Should().EndWith("# c");
    }

    // ----- FormatRange: Format Selection's seam (P22e decisions 1–2, #2168) -----
    // Documents and cell numbers are plan P22e's. Each expected text is Format(D) restricted to the
    // hunks the selection touches; AssertRange also checks it is LineDiff.Apply of hunks of Format(D).

    private const string D1 = "def helper() -> int:\n    return 2\ndef main():\n    x   =   helper()\n    print(x)\n";
    private const string D1Formatted = "def helper() -> int:\n    return 2\n\n\ndef main():\n    x = helper()\n    print(x)\n";
    private const string D1BodyOnly = "def helper() -> int:\n    return 2\ndef main():\n    x = helper()\n    print(x)\n";
    private const string D2 = "def main():\n    xs = [1,\n          2]\n    # keep me\n    y = 3\n    print(xs, y)\n";
    private const string D2Formatted = "def main():\n    xs = [1, 2]\n    # keep me\n    y = 3\n    print(xs, y)\n";
    private const string D3 = "def f(): ...\ndef main():\n    x   =  1\n    print(x)\n";
    private const string D4 = "def main():\n    x = 1 + \\\n        2\n    y   =  3\n    print(x, y)\n";
    private const string D9 = "# top\ndef a():\n    return 1  # one\n# between\ndef b():\n    # inside\n    return 2\n# tail\n";
    private const string D11 = "def main():\n        if False:\n                print(1)\n                print(2)\n";
    private const string D16 = "def main():\n        x = 1\n\n        y = 2\n        print(x, y)\n";
    private const string D17 = "import os\nx: int = 1\n\n\n\n\n\ndef main():\n    print(x)\n";

    /// <summary>A selection of whole lines <c>s..e</c> ending inside line <c>e</c> (not at column 0).</summary>
    private static FormatSelection Lines(int s, int e) => new(s, e, EndCharacter: 1);

    private static FormatRangeResult AssertRange(string source, FormatSelection selection, string expectedApplied)
    {
        var result = FormatterService.FormatRange(source, selection);

        result.Diagnostics.Should().BeEmpty();
        result.SourceParses.Should().BeTrue();
        result.AppliedText.Should().Be(expectedApplied);
        LineDiff.Apply(source, result.Hunks).Should().Be(result.AppliedText);
        FormatterService.CheckApplied(source, result.AppliedText, out var sourceParses).Should().BeNull("the applied text parses and passes the net");
        sourceParses.Should().BeTrue();
        var all = LineDiff.Hunks(source, FormatterService.Format(source).FormattedText);
        foreach (var hunk in result.Hunks)
        {
            all.Should().Contain(h => h.Start == hunk.Start && h.End == hunk.End && h.NewLines.SequenceEqual(hunk.NewLines),
                "every returned hunk is a hunk of the checked whole-document output");
        }

        return result;
    }

    private static void AssertRefused(string source, FormatSelection selection)
    {
        var result = FormatterService.FormatRange(source, selection);

        result.SourceParses.Should().BeTrue();
        result.Hunks.Should().BeEmpty();
        result.AppliedText.Should().Be(source);
        result.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.Infrastructure.FormatterDeclined);
        FormatterService.Format(source).Diagnostics.Should().BeEmpty("the whole-document output passes the net — only the selected subset is refused");
    }

    [Fact]
    public void FormatRange_Cell1_MainsBody_RespacesLine3_AddsNoBlankLinesAboveDef()
        => AssertRange(D1, Lines(3, 4), D1BodyOnly).Hunks.Should().ContainSingle().Which.Start.Should().Be(3);

    [Fact]
    public void FormatRange_Cell2_D2Lines1To4_IsFormatDocument()
        => AssertRange(D2, Lines(1, 4), D2Formatted);

    [Fact]
    public void FormatRange_Cell3_D2Lines2To3_AppliesTheListJoinWhole()
        => AssertRange(D2, Lines(2, 3), D2Formatted);

    [Fact]
    public void FormatRange_Cell4_D1WholeDocument_IsFormatDocument()
    {
        AssertRange(D1, Lines(0, 5), D1Formatted);
        AssertRange(D1, new FormatSelection(0, 5, EndCharacter: 0), D1Formatted);
        AssertRange(D1, Lines(0, 4), D1Formatted).Hunks.Should().HaveCount(2, "the last line selected takes the empty final line too");
    }

    [Fact]
    public void FormatRange_Cell5_D9WholeDocument_IsFormatDocument_KeepsTheTailComment()
        => AssertRange(D9, Lines(0, 8),
            "# top\ndef a():\n    return 1  # one\n\n\n# between\ndef b():\n    # inside\n    return 2\n\n\n# tail\n");

    [Fact]
    public void FormatRange_Cell6_D3Line2_RespacesOnlyLine2()
        => AssertRange(D3, Lines(2, 2), "def f(): ...\ndef main():\n    x = 1\n    print(x)\n");

    /// <summary>
    /// Plan cell 7 expects "line 3 re-spaced"; the backslash join of lines 1–2 and the re-spacing of
    /// line 3 have no unchanged line between them, so they are ONE hunk (lines are compared by content)
    /// and selecting line 3 applies both — Format(D4), never a duplicated line.
    /// </summary>
    [Fact]
    public void FormatRange_Cell7_D4Line3_TakesTheWholeHunkItTouches()
        => AssertRange(D4, Lines(3, 3), "def main():\n    x = 1 + 2\n    y = 3\n    print(x, y)\n");

    [Fact]
    public void FormatRange_Cell8_D17Lines7To8_Unchanged()
        => AssertRange(D17, Lines(7, 8), D17).Hunks.Should().BeEmpty();

    /// <summary>
    /// Plan cell 9 (D16, line 1). Format deletes the blank line inside main, so lines 1–4 differ with no
    /// unchanged line between them: one hunk, and selecting line 1 takes the whole re-indent — which
    /// passes the net. (At 7a034b81d this request wrote `x` at 4 and left `y` at 8.)
    /// </summary>
    [Fact]
    public void FormatRange_Cell9_D16Line1_TakesTheWholeReindentHunk()
    {
        var hunk = AssertRange(D16, Lines(1, 1), "def main():\n    x = 1\n    y = 2\n    print(x, y)\n").Hunks.Should().ContainSingle().Subject;
        (hunk.Start, hunk.End).Should().Be((1, 5));
    }

    /// <summary>Plan cell 10 (D11, line 3): one hunk over lines 1–3, so "the whole re-indent".</summary>
    [Fact]
    public void FormatRange_Cell10_D11Line3_TakesTheWholeReindentHunk()
        => AssertRange(D11, Lines(3, 3), "def main():\n    if False:\n        print(1)\n        print(2)\n");

    /// <summary>
    /// A selected subset the net refuses: D17's hunks are "delete line 1" and "replace lines 3–4 with
    /// `x: int = 1`" (the LCS pairs the blank line, not the declaration). Selecting line 1 alone would
    /// delete the declaration — the whole-document output passes the net, the applied text does not.
    /// </summary>
    [Fact]
    public void FormatRange_D17Line1_SubsetDeletesTheDeclaration_Refused()
        => AssertRefused(D17, Lines(1, 1));

    /// <summary>A selected subset that does not re-parse: line 1 re-indented to 4, `print(s)` left at 8 (the string's lines are unchanged).</summary>
    [Fact]
    public void FormatRange_SubsetThatDoesNotReparse_Refused()
        => AssertRefused("def main():\n        s = \"\"\"\nabc\n\"\"\"\n        print(s)\n", Lines(1, 1));

    [Fact]
    public void FormatRange_SelectionEndingAtColumn0_ExcludesThatLine()
    {
        // (2,0)–(3,0) selects line 2 only: neither the re-spacing of line 3 nor the insertion before line 2.
        AssertRange(D1, new FormatSelection(2, 3, EndCharacter: 0), D1).Hunks.Should().BeEmpty();
        // Ending inside line 3 takes it.
        AssertRange(D1, Lines(2, 3), D1BodyOnly);
        // (3,0)–(4,0) is line 3.
        AssertRange(D1, new FormatSelection(3, 4, EndCharacter: 0), D1BodyOnly);
    }

    [Fact]
    public void FormatRange_InsertionNeedsBothNeighboursSelected()
    {
        // Lines 1..2 hold both neighbours of the insertion before line 2 — the blank lines are added, line 3 is not touched.
        AssertRange(D1, Lines(1, 2), "def helper() -> int:\n    return 2\n\n\ndef main():\n    x   =   helper()\n    print(x)\n");
        // Line 2 alone holds only one neighbour.
        AssertRange(D1, Lines(2, 2), D1).Hunks.Should().BeEmpty();
    }

    [Fact]
    public void FormatRange_CrlfDocument_StaysCrlf()
    {
        var crlf = D1.Replace("\n", "\r\n");
        AssertRange(crlf, Lines(3, 4), D1BodyOnly.Replace("\n", "\r\n"));
        AssertRange(crlf, Lines(0, 5), D1Formatted.Replace("\n", "\r\n"));
    }

    [Fact]
    public void FormatRange_SourceDoesNotParse_NoHunks_ReturnsTheErrors()
    {
        var source = D11 + "        x = (\n";
        var result = FormatterService.FormatRange(source, Lines(0, 4));

        result.SourceParses.Should().BeFalse();
        result.Hunks.Should().BeEmpty();
        result.AppliedText.Should().Be(source);
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.FormatterDeclined);
    }

    [Fact]
    public void CheckApplied_SourceParses_RunsTheNetOnTheAppliedText()
    {
        FormatterService.CheckApplied(D1, D1BodyOnly, out var parses).Should().BeNull();
        parses.Should().BeTrue();

        var deleted = "import os\n\n\n\n\n\ndef main():\n    print(x)\n";
        var verdict = FormatterService.CheckApplied(D17, deleted, out parses);
        parses.Should().BeTrue();
        verdict.Should().NotBeNull();
        verdict!.Code.Should().Be(DiagnosticCodes.Infrastructure.FormatterDeclined);
    }

    [Fact]
    public void CheckApplied_SourceDoesNotParse_ReturnsNull_CallerChecks()
    {
        var source = D11 + "        x = (\n";
        FormatterService.CheckApplied(source, "garbage (\n", out var parses).Should().BeNull();
        parses.Should().BeFalse();

        // Positive control: the same applied text against a source that parses is refused.
        FormatterService.CheckApplied(D11, "garbage (\n", out parses).Should().NotBeNull();
        parses.Should().BeTrue();
    }
}

