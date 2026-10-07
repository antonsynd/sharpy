using System.Text;
using FluentAssertions;
using Sharpy.Compiler.Pretty;
using Sharpy.TestInfrastructure.Formatting;
using Xunit;
using Xunit.Abstractions;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SModule = Sharpy.Compiler.Parser.Ast.Module;
using SParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The twin builders of <see cref="FormatterTwins"/> on hand snippets: every injected position and
/// count is pinned literally, and each twin parses clean (T1 to the same AST as its source).
/// </summary>
public class FormatterTwinsTests
{
    private readonly ITestOutputHelper _output;

    public FormatterTwinsTests(ITestOutputHelper output) => _output = output;

    private const string Snippet =
        "def f(a: int, b: int) -> list[int]:\n"
        + "    if a > b:\n"
        + "        return [a,\n"
        + "                b]\n"
        + "    elif a:  # kept\n"
        + "        x = (a\n"
        + "             + b)\n"
        + "    else:\n"
        + "        print(f\"{a + b}\")\n"
        + "    return []\n";

    private const string Clauses =
        "@dec\n"
        + "def g(n: int) -> int:\n"
        + "    try:\n"
        + "        s = \"\"\"x\n"
        + "y\"\"\"  # note\n"
        + "        t = 1 + \\\n"
        + "            2\n"
        + "    except ValueError as e:\n"
        + "        pass\n"
        + "    finally:\n"
        + "        pass\n"
        + "    match n:\n"
        + "        case 1:\n"
        + "            return 1\n"
        + "        case _:\n"
        + "            for i in xs:\n"
        + "                pass\n"
        + "            else:\n"
        + "                return 0\n"
        + "    return 2";

    /// <summary>
    /// Brackets (open, comma at depth ≥ 1, close — in a parameter list, a return annotation, a list
    /// display, a parenthesised continuation and a call around an f-string whose hole is left
    /// alone), the line-total kind on header, statement and bracket-continuation lines, clause
    /// colons and keywords, block ends and end of file. The existing <c># kept</c> is not
    /// swallowed: its line gets no appended comment.
    /// </summary>
    [Fact]
    public void CommentInjected_Snippet_InjectsAtEveryAnchorKind()
    {
        var (text, counts) = FormatterTwins.CommentInjected(Snippet);

        text.Should().Be(Lines(
            "# c1",
            "def f(  # c2",
            "a: int,  # c3",
            " b: int",
            "# c4",
            ") -> list[  # c5",
            "int",
            "# c6",
            "]:  # c7",
            "    # c8",
            "    if a > b:  # c9",
            "        # c10",
            "        return [  # c11",
            "a,  # c12",
            "",
            "                # c13",
            "                b",
            "# c14",
            "]  # c15",
            "        # c16",
            "    # c17",
            "    elif a:  # kept",
            "        # c18",
            "        x = (  # c19",
            "a  # c20",
            "             # c21",
            "             + b",
            "# c22",
            ")  # c23",
            "        # c24",
            "    # c25",
            "    else:  # c26",
            "        # c27",
            "        print(  # c28",
            "f\"{a + b}\"",
            "# c29",
            ")  # c30",
            "        # c31",
            "    # c32",
            "    return [  # c33",
            "",
            "# c34",
            "]  # c35",
            "    # c36",
            "# c37",
            ""));
        Counts(counts).Should().Equal(6, 2, 6, 1, 2, 4, 1, 6, 1, 8);
        counts.Total.Should().Be(37);
    }

    /// <summary>
    /// Every clause keyword (<c>except</c>, <c>finally</c>, <c>case</c> ×2, a <c>for</c> loop's
    /// <c>else</c>), nine blocks closed (three at once at the end of the <c>match</c>), a decorator
    /// line, and the three lines that must NOT get an appended comment: one ending inside a
    /// triple-quoted string, one already ending in a comment, one ending in a backslash
    /// continuation (whose next line also gets no own-line comment). The source has no final line
    /// break; the block-end and end-of-file comments start their own lines.
    /// </summary>
    [Fact]
    public void CommentInjected_Clauses_InjectsAtEveryClauseAndSkipsUnsafeLineEnds()
    {
        var (text, counts) = FormatterTwins.CommentInjected(Clauses);

        text.Should().Be(Lines(
            "# c1",
            "@dec  # c2",
            "# c3",
            "def g(  # c4",
            "n: int",
            "# c5",
            ") -> int:  # c6",
            "    # c7",
            "    try:  # c8",
            "        # c9",
            "        s = \"\"\"x",
            "y\"\"\"  # note",
            "        # c10",
            "        t = 1 + \\",
            "            2  # c11",
            "        # c12",
            "    # c13",
            "    except ValueError as e:  # c14",
            "        # c15",
            "        pass  # c16",
            "        # c17",
            "    # c18",
            "    finally:  # c19",
            "        # c20",
            "        pass  # c21",
            "        # c22",
            "    # c23",
            "    match n:  # c24",
            "        # c25",
            "        case 1:  # c26",
            "            # c27",
            "            return 1  # c28",
            "            # c29",
            "        # c30",
            "        case _:  # c31",
            "            # c32",
            "            for i in xs:  # c33",
            "                # c34",
            "                pass  # c35",
            "                # c36",
            "            # c37",
            "            else:  # c38",
            "                # c39",
            "                return 0  # c40",
            "                # c41",
            "            # c42",
            "        # c43",
            "    # c44",
            "    return 2  # c45",
            "    # c46",
            "# c47",
            ""));
        Counts(counts).Should().Equal(1, 0, 1, 5, 5, 9, 1, 12, 0, 13);
        counts.Total.Should().Be(47);
    }

    /// <summary>The twin is the same program: it parses clean, to the same normalized AST, with exactly the injected comments added.</summary>
    [Theory]
    [InlineData(Snippet)]
    [InlineData(Clauses)]
    public void CommentInjected_TwinParsesToTheSameAstWithTheInjectedCommentsAdded(string source)
    {
        var (text, counts) = FormatterTwins.CommentInjected(source);

        Parse(source, out var sourceErrors);
        Parse(text, out var twinErrors);
        sourceErrors.Should().BeFalse();
        twinErrors.Should().BeFalse(text);
        SameAst(source, text).Should().BeTrue(text);
        FormatterTwins.Comments(text).Count.Should().Be(FormatterTwins.Comments(source).Count + counts.Total);
    }

    [Fact]
    public void CommentInjected_CrlfSource_InjectsCrlfLineBreaks()
    {
        var (text, counts) = FormatterTwins.CommentInjected("if x:\r\n    f(1)\r\n");

        text.Should().Be("# c1\r\nif x:  # c2\r\n    # c3\r\n    f(  # c4\r\n1\r\n# c5\r\n)  # c6\r\n    # c7\r\n# c8\r\n");
        counts.Total.Should().Be(8);
    }

    /// <summary>
    /// Every unescaped identifier — including <c>int</c>/<c>print</c> and one inside an f-string
    /// hole — is wrapped; an already escaped name and the contextual <c>before_set</c> are not.
    /// </summary>
    [Fact]
    public void BacktickInjected_WrapsEveryUnescapedIdentifierExceptTheContextualOnes()
    {
        const string source = "def f(`class`: int, before_set: int) -> int:\n    return print(f\"{`class` + before_set + k}\")\n";

        var (text, count) = FormatterTwins.BacktickInjected(source);

        text.Should().Be("def `f`(`class`: `int`, before_set: `int`) -> `int`:\n    return `print`(f\"{`class` + before_set + `k`}\")\n");
        count.Should().Be(6);
        FormatterTwins.EscapedNames(text).Should().Equal("class", "class", "f", "int", "int", "int", "k", "print");
    }

    /// <summary>
    /// A wildcard <c>case _:</c> is escaped like any identifier since #2166 (the escaped twin binds
    /// <c>`_`</c> and parses), and nothing is left bare; the same spellings as a plain identifier
    /// are escaped too.
    /// </summary>
    [Fact]
    public void BacktickInjected_EscapesACaseWildcard_AndLeavesNothingBare()
    {
        const string source = "def f(x: int) -> int:\n    match x:\n        case _:\n            return d.get(out, x)\n";

        var (text, count) = FormatterTwins.BacktickInjected(source);

        text.Should().Be("def `f`(`x`: `int`) -> `int`:\n    match `x`:\n        case `_`:\n            return `d`.`get`(`out`, `x`)\n");
        count.Should().Be(10);
        FormatterTwins.ContextualKeywordSkips(source).Should().BeEmpty();
        Parse(text, out var errors);
        errors.Should().BeFalse(text);
    }

    /// <summary>
    /// One cell per contextual site that T2 leaves bare: the value is not escaped there, and the
    /// escaped spelling at that site — an identifier since #2166 — would not parse (the positive
    /// reason for the skip; the rest of the twin parses).
    /// </summary>
    [Theory]
    [InlineData("get", "class C:\n    property get p(self) -> int:\n        return 1\n")]
    [InlineData("set", "class C:\n    property set p(self, v: int):\n        pass\n")]
    [InlineData("init", "class C:\n    property init p(self, v: int):\n        pass\n")]
    [InlineData("add", "class C:\n    event add e(self, h: H):\n        pass\n")]
    [InlineData("remove", "class C:\n    event remove e(self, h: H):\n        pass\n")]
    [InlineData("when", "try:\n    pass\nexcept E as e when c:\n    pass\n")]
    [InlineData("out", "interface I[out T]:\n    def g(self) -> T\n")]
    [InlineData("out", "def f(x: out int) -> None:\n    pass\n")]
    [InlineData("out", "def main():\n    f(out y)\n")]
    [InlineData("ref", "def f(x: ref int) -> None:\n    pass\n")]
    [InlineData("ref", "def main():\n    f(ref y)\n")]
    public void BacktickInjected_LeavesAContextualKeywordBare_AtItsSite_WhereTheEscapeWouldNotParse(string value, string source)
    {
        Parse(source, out var sourceErrors);
        sourceErrors.Should().BeFalse(source);

        var (text, _) = FormatterTwins.BacktickInjected(source);

        text.Should().NotContain($"`{value}`");
        FormatterTwins.ContextualKeywordSkips(source).Should().BeEquivalentTo(new Dictionary<string, int> { [value] = 1 });
        Parse(text, out var errors);
        errors.Should().BeFalse(text);

        var escapedAtSite = EscapeTheTokenAtItsContextualSite(text);
        escapedAtSite.Should().Contain($"`{value}`");
        Parse(escapedAtSite, out var escapedErrors);
        escapedErrors.Should().BeTrue($"the honoured escape makes '{value}' an identifier at its site, which does not parse:\n{escapedAtSite}");
    }

    /// <summary>
    /// <c>_</c> and <c>notnull</c> at their former contextual sites, and every contextual-keyword
    /// spelling as a plain identifier, are escaped like any other identifier, and the twin parses.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    f(1, _)\n", new[] { "_" })]
    [InlineData("def main():\n    g = f(1, b=_)\n", new[] { "_" })]
    [InlineData("def main():\n    h = (_ + 1)\n", new[] { "_" })]
    [InlineData("def main():\n    match x:\n        case [1, *_]:\n            pass\n", new[] { "_" })]
    [InlineData("def f[T: notnull](x: T) -> T:\n    return x\n", new[] { "notnull" })]
    [InlineData("def f[T: A, notnull](x: T) -> T:\n    return x\n", new[] { "notnull" })]
    [InlineData("def main():\n    xs.add(set)\n    init = ref\n    when = notnull\n    remove = d.get(out)\n",
        new[] { "add", "set", "init", "ref", "when", "notnull", "remove", "get", "out" })]
    public void BacktickInjected_EscapesTheSameSpellingsAsPlainIdentifiers(string source, string[] values)
    {
        var (text, _) = FormatterTwins.BacktickInjected(source);

        foreach (var value in values)
            text.Should().Contain($"`{value}`");
        FormatterTwins.ContextualKeywordSkips(source).Should().BeEmpty();
        Parse(text, out var errors);
        errors.Should().BeFalse(text);
    }

    [Fact]
    public void BacktickInjected_SnippetTwinParses()
    {
        var (text, count) = FormatterTwins.BacktickInjected(Snippet);

        count.Should().Be(18);
        Parse(text, out var errors);
        errors.Should().BeFalse(text);
        FormatterTwins.EscapedNames(text).Count.Should().Be(18);
    }

    /// <summary>Leading, same-line trailing (reclassified onto the comma and the bracket) and end-of-file comments, in source order.</summary>
    [Fact]
    public void Comments_AreInSourceOrder()
    {
        FormatterTwins.Comments("# a\nxs = [1,  # b\n      2]  # c\n# d\n")
            .Should().Equal("# a", "# b", "# c", "# d");
    }

    /// <summary>
    /// A comment's trailing spaces/tabs are not compared: the lexer keeps them (positive control on
    /// the raw trivia) and <c>StripTrailingWhitespace</c> removes them by design, so the formatted
    /// text's comment sequence equals the source's.
    /// </summary>
    [Fact]
    public void Comments_IgnoreTrailingWhitespace_WhichTheLexerKeepsAndTheFormatterStrips()
    {
        const string source = "# own  \nx = 1  # t \t \n";
        var raw = FormatterTwins.Lex(source, out _)
            .SelectMany(t => (t.LeadingTrivia ?? Array.Empty<Sharpy.Compiler.Lexer.Trivia>()).Concat(t.TrailingTrivia ?? Array.Empty<Sharpy.Compiler.Lexer.Trivia>()))
            .Select(t => t.Text);
        raw.Should().Equal("# own  ", "# t \t ");

        FormatterTwins.Comments(source).Should().Equal("# own", "# t");
        var formatted = Sharpy.Compiler.Formatting.FormatterService.Format(source).FormattedText;
        formatted.Should().NotContain("# t \t");
        FormatterTwins.Comments(formatted).Should().Equal(FormatterTwins.Comments(source));
    }

    [Fact]
    public void EscapedNames_IsTheSortedMultisetOfEscapedIdentifiers()
    {
        FormatterTwins.EscapedNames("`b` = `a` + `b` + c\n").Should().Equal("a", "b", "b");
    }

    // ================================================================
    // Layout twins: wide-indent and CRLF (P22e, #2168)
    // ================================================================

    private const string Layout =
        "# top\n"
        + "def f(a: int,\n"
        + "      b: int) -> int:\n"
        + "    # inside\n"
        + "    s = \"\"\"x\n"
        + "    y\"\"\"\n"
        + "\n"
        + "    if a:\n"
        + "        t = [1,\n"
        + "             2]\n"
        + "        u = 1 + \\\n"
        + "            2\n"
        + "      # shallow\n"
        + "    return a\n";

    /// <summary>
    /// Pinned literally — the positive control that the twin differs from its source: logical-line
    /// starts and comment-only lines (<c># inside</c>, the off-level <c># shallow</c>) are doubled;
    /// the bracket continuations of <c>def f(</c> (first line unindented, so nothing added) and of
    /// <c>t = [</c>, and the backslash continuation of <c>u = 1 + \</c>, get their statement's
    /// added 8 spaces; the literal-interior line <c>    y"""</c> and the blank line are untouched.
    /// </summary>
    [Fact]
    public void WideIndented_Snippet_DoublesLineStartsShiftsContinuationsAndLeavesLiteralsAlone()
    {
        var text = FormatterTwins.WideIndented(Layout);

        // The AST first: a doubled literal-interior line changes the string's value.
        Parse(text, out var errors);
        errors.Should().BeFalse(text);
        SameAst(Layout, text).Should().BeTrue(text);
        text.Should().Be(Lines(
            "# top",
            "def f(a: int,",
            "      b: int) -> int:",
            "        # inside",
            "        s = \"\"\"x",
            "    y\"\"\"",
            "",
            "        if a:",
            "                t = [1,",
            "                     2]",
            "                u = 1 + \\",
            "                    2",
            "            # shallow",
            "        return a",
            ""));
        text.Should().NotBe(Layout);
    }

    /// <summary>The wide twin keeps a CRLF source's breaks; the CRLF twin writes every break — <c>\n</c> or <c>\r\n</c>, in a literal too — as <c>\r\n</c>, and parses to the same AST.</summary>
    [Fact]
    public void WideIndented_KeepsCrlfBreaks_AndCrlf_RewritesEveryBreak()
    {
        FormatterTwins.WideIndented("if x:\r\n    f(1,\r\n      2)\r\n").Should().Be("if x:\r\n        f(1,\r\n          2)\r\n");

        const string mixed = "a = 1\nb = \"\"\"x\r\ny\nz\"\"\"\r\nc = 2";
        var crlf = FormatterTwins.Crlf(mixed);

        crlf.Should().Be("a = 1\r\nb = \"\"\"x\r\ny\r\nz\"\"\"\r\nc = 2");
        Parse(crlf, out var errors);
        errors.Should().BeFalse(crlf);
        SameAst(mixed, crlf).Should().BeTrue(crlf);
    }

    /// <summary>
    /// Instrument check over the formatter sweep's corpus (every single-file fixture that parses):
    /// the wide twin parses to the source's AST, keeps every line's number, and leaves every line
    /// starting inside a literal byte-identical — two counts, because the AST alone misses a
    /// uniformly re-indented d-string (its dedent absorbs it). Positive controls: the twin differs
    /// from the source on most fixtures, and literal-interior lines with leading whitespace — the
    /// lines a re-indent would damage — are in the corpus and checked.
    /// </summary>
    [Fact]
    public void WideIndented_Corpus_ParsesToTheSourceAst_AndKeepsLiteralLinesByteIdentical()
    {
        var corpus = FormatterMeaningPreservationSweepTests.CorpusCensus.Value.Corpus;
        var astFailures = new System.Collections.Concurrent.ConcurrentBag<string>();
        var literalFailures = new System.Collections.Concurrent.ConcurrentBag<string>();
        var widened = 0;
        var literalLinesChecked = 0;
        var indentedLiteralLinesChecked = 0;
        Parallel.ForEach(corpus.Values, fixture =>
        {
            var source = fixture.Source;
            var wide = FormatterTwins.WideIndented(source);
            if (wide != source)
                Interlocked.Increment(ref widened);

            Parse(wide, out var errors);
            if (errors)
                astFailures.Add($"{fixture.Name}: the wide twin does not parse");
            else if (!SameAst(source, wide))
                astFailures.Add($"{fixture.Name}: the wide twin parses to a different AST");

            var sourceLines = SplitLines(source);
            var wideLines = SplitLines(wide);
            if (sourceLines.Count != wideLines.Count)
            {
                literalFailures.Add($"{fixture.Name}: the wide twin has {wideLines.Count} lines, the source {sourceLines.Count}");
                return;
            }

            var literalLines = Sharpy.Compiler.Lexer.LiteralSpans.LinesStartingInside(
                source, Sharpy.Compiler.Lexer.LiteralSpans.Of(FormatterTwins.Lex(source, out _)));
            foreach (var line in literalLines)
            {
                Interlocked.Increment(ref literalLinesChecked);
                if (sourceLines[line - 1].Length > 0 && sourceLines[line - 1][0] is ' ' or '\t')
                    Interlocked.Increment(ref indentedLiteralLinesChecked);
                if (wideLines[line - 1] != sourceLines[line - 1])
                    literalFailures.Add($"{fixture.Name}: literal line {line} changed");
            }
        });

        _output.WriteLine($"corpus={corpus.Count} widened={widened} literal-lines={literalLinesChecked} indented-literal-lines={indentedLiteralLinesChecked} ast-failures={astFailures.Count} literal-line-failures={literalFailures.Count}");
        foreach (var failure in astFailures.Concat(literalFailures).OrderBy(f => f, StringComparer.Ordinal))
            _output.WriteLine($"  {failure}");
        astFailures.Should().BeEmpty();
        literalFailures.Should().BeEmpty();
        widened.Should().BeGreaterThan(corpus.Count / 2, "most fixtures have an indented block, so the twin differs from them");
        indentedLiteralLinesChecked.Should().BeGreaterThan(0, "the corpus holds literal-interior lines with leading whitespace");
    }

    /// <summary>Instrument check over the corpus: the CRLF twin parses to the source's AST and has no line break but <c>\r\n</c>. Positive control: it differs from the source on every fixture with a line break.</summary>
    [Fact]
    public void Crlf_Corpus_ParsesToTheSourceAst() => LineBreakTwin_Corpus_ParsesToTheSourceAst(FormatterTwins.Crlf, "\r\n");

    /// <summary>Instrument check over the corpus: the CR twin parses to the source's AST and has no line break but a lone <c>\r</c>. Positive control: it differs from the source on every fixture with a line break.</summary>
    [Fact]
    public void Cr_Corpus_ParsesToTheSourceAst() => LineBreakTwin_Corpus_ParsesToTheSourceAst(FormatterTwins.Cr, "\r");

    /// <summary>The CR twin writes every break — <c>\n</c> or <c>\r\n</c>, in a literal too — as a lone <c>\r</c>, and parses to the same AST.</summary>
    [Fact]
    public void Cr_RewritesEveryBreak()
    {
        const string mixed = "a = 1\nb = \"\"\"x\r\ny\nz\"\"\"\r\nc = 2";
        var cr = FormatterTwins.Cr(mixed);

        cr.Should().Be("a = 1\rb = \"\"\"x\ry\rz\"\"\"\rc = 2");
        Parse(cr, out var errors);
        errors.Should().BeFalse(cr);
        SameAst(mixed, cr).Should().BeTrue(cr);
    }

    private void LineBreakTwin_Corpus_ParsesToTheSourceAst(Func<string, string> twin, string lineBreak)
    {
        var corpus = FormatterMeaningPreservationSweepTests.CorpusCensus.Value.Corpus;
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        var changed = 0;
        Parallel.ForEach(corpus.Values, fixture =>
        {
            var source = fixture.Source;
            var text = twin(source);
            if (text != source)
                Interlocked.Increment(ref changed);
            if (HasOtherBreak(text, lineBreak))
                failures.Add($"{fixture.Name}: a line break is not {Escape(lineBreak)}");

            Parse(text, out var errors);
            if (errors)
                failures.Add($"{fixture.Name}: the {Escape(lineBreak)} twin does not parse");
            else if (!SameAst(source, text))
                failures.Add($"{fixture.Name}: the {Escape(lineBreak)} twin parses to a different AST");
        });

        _output.WriteLine($"corpus={corpus.Count} changed={changed} failures={failures.Count}");
        failures.Should().BeEmpty();
        changed.Should().Be(corpus.Values.Count(f => HasOtherBreak(f.Source, lineBreak)));
        changed.Should().BeGreaterThan(corpus.Count / 2, "the corpus is written with \\n breaks");

        static string Escape(string s) => s.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
    }

    /// <summary>
    /// The stems on which <c>Format(Wide(P)) != Format(P)</c>, skipped by the check below and
    /// counted — the twin is not weakened for them. Each has an own-line comment inside the brackets
    /// of a statement: the twin doubles that comment-only line's leading whitespace while the
    /// bracket's other continuation lines get only their statement's added width, and a statement
    /// with a comment inside its brackets is written verbatim (P22b Decision 5), so the comment's
    /// moved column survives formatting. The set is exact both ways: a new stem or a drained one is
    /// red (#2168).
    /// </summary>
    private static readonly IReadOnlySet<string> WideFormatDiffers = new HashSet<string>(StringComparer.Ordinal)
    {
        "Formatting/inner_comments_verbatim",
        "imports/from_import_parenthesized",
    };

    /// <summary>
    /// Instrument check over the corpus: the formatter reads the wide twin as the same layout —
    /// <c>Format(Wide(P)) == Format(P)</c>, except on the counted <see cref="WideFormatDiffers"/>
    /// stems. Positive control: on the fixtures where the twin differs from the source, the
    /// formatter output is compared, not the input.
    /// </summary>
    [Fact]
    public void WideIndented_Corpus_FormatsToTheSourceFormat()
    {
        var corpus = FormatterMeaningPreservationSweepTests.CorpusCensus.Value.Corpus;
        var differing = new System.Collections.Concurrent.ConcurrentBag<string>();
        var compared = 0;
        Parallel.ForEach(corpus.Values, fixture =>
        {
            var source = fixture.Source;
            var wide = FormatterTwins.WideIndented(source);
            if (wide == source)
                return;
            Interlocked.Increment(ref compared);
            var expected = Sharpy.Compiler.Formatting.FormatterService.Format(source).FormattedText;
            var actual = Sharpy.Compiler.Formatting.FormatterService.Format(wide).FormattedText;
            if (actual != expected)
                differing.Add(fixture.Name);
        });

        var stems = differing.OrderBy(s => s, StringComparer.Ordinal).ToList();
        _output.WriteLine($"corpus={corpus.Count} compared={compared} format-differs={stems.Count} skipped-stems={stems.Count(WideFormatDiffers.Contains)}");
        foreach (var stem in stems)
            _output.WriteLine($"  {stem}");
        compared.Should().BeGreaterThan(corpus.Count / 2);
        stems.Should().BeEquivalentTo(WideFormatDiffers, "a stem outside the counted set is a new layout difference; a counted stem that formats equal is drained");
    }

    /// <summary>Whether <paramref name="text"/> has a line break (<c>\r\n</c>, <c>\n</c> or a lone <c>\r</c>) other than <paramref name="lineBreak"/>.</summary>
    private static bool HasOtherBreak(string text, string lineBreak)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c != '\n' && c != '\r')
                continue;
            var crlf = c == '\r' && i + 1 < text.Length && text[i + 1] == '\n';
            var found = crlf ? "\r\n" : c == '\n' ? "\n" : "\r";
            if (found != lineBreak)
                return true;
            if (crlf)
                i++;
        }

        return false;
    }

    /// <summary>The lines of a text split at <c>\r\n</c>, <c>\n</c> or a lone <c>\r</c>, as in the lexer.</summary>
    private static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c != '\n' && c != '\r')
                continue;
            lines.Add(text[start..i]);
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                i++;
            start = i + 1;
        }

        lines.Add(text[start..]);
        return lines;
    }

    private static string Lines(params string[] lines) => string.Join("\n", lines);

    private static IEnumerable<int> Counts(InjectedCommentCounts counts)
        => InjectedCommentCounts.Kinds.Select(k => counts[k]);

    /// <summary>The text with every unescaped token T2 left bare at its contextual site wrapped in backticks.</summary>
    private static string EscapeTheTokenAtItsContextualSite(string text)
    {
        var tokens = FormatterTwins.Lex(text, out _);
        var result = new StringBuilder(text);
        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            var token = tokens[i];
            if (token.IsBacktickEscaped || !FormatterTwins.IsAtContextualSite(tokens, i))
                continue;
            result.Insert(token.Position + token.Length, '`');
            result.Insert(token.Position, '`');
        }

        return result.ToString();
    }

    private static SModule Parse(string source, out bool hasErrors)
    {
        var lexer = new SLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new SParser(tokens);
        var module = parser.ParseModule();
        hasErrors = lexer.Diagnostics.HasErrors || parser.Diagnostics.HasErrors;
        return module;
    }

    private static bool SameAst(string a, string b)
        => StructuralEqualityComparer.Instance.Equals(
            AstNormalizer.Instance.NormalizeModule(Parse(a, out _)),
            AstNormalizer.Instance.NormalizeModule(Parse(b, out _)));
}
