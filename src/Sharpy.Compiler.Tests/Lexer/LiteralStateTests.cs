using FluentAssertions;
using Sharpy.TestInfrastructure.Formatting;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// <see cref="LexerNs.Lexer.LiteralStateUnknown"/> (P22e decision 7, #2168): the lexer records that it
/// aborted while reading a literal that can span lines — any triple-quoted read, or an f-/t-string that is
/// triple-quoted or began on an earlier line — because error recovery then resumes on the next line as
/// code and the token stream's map of string lines is a guess for the rest of the document. A
/// single-quoted literal ends at its line and sets nothing. The fact changes no token or diagnostic.
/// </summary>
public class LiteralStateTests
{
    private readonly ITestOutputHelper _output;

    public LiteralStateTests(ITestOutputHelper output) => _output = output;

    /// <summary>Every string-literal prefix, spelled out (not read from the lexer's table — see <see cref="Rows_CoverEveryLexerPrefix"/>).</summary>
    private static readonly string[] Prefixes = { "", "d", "r", "dr", "b", "f", "t", "df" };

    private static readonly string[] TripleQuotes = { "\"\"\"", "'''" };

    public static IEnumerable<object[]> PrefixByTripleQuote()
        => Prefixes.SelectMany(p => TripleQuotes.Select(q => new object[] { p, q }));

    public static IEnumerable<object[]> AllPrefixes() => Prefixes.Select(p => new object[] { p });

    private static LexerNs.Lexer Lex(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        lexer.TokenizeAll();
        return lexer;
    }

    private static string Errors(LexerNs.Lexer lexer)
        => string.Join(" | ", lexer.Diagnostics.GetErrors().Select(d => $"{d.Code} {d.Message} @{d.Line}:{d.Column}"));

    [Fact]
    public void Rows_CoverEveryLexerPrefix()
    {
        Prefixes.Should().BeEquivalentTo(LexerNs.Lexer.StringLiteralPrefixes,
            "a new string prefix needs its rows here: a new read path is only covered once a test cuts it");
    }

    // ---------------------------------------------------------------- the two cut shapes

    /// <summary>D5u: the document ends after the literal's first interior line.</summary>
    [Theory]
    [MemberData(nameof(PrefixByTripleQuote))]
    public void CutAfterFirstInteriorLine_SetsTheFact(string prefix, string quotes)
    {
        var lexer = Lex($"def main():\n    s = {prefix}{quotes}\n        key: value\n");

        lexer.Diagnostics.HasErrors.Should().BeTrue();
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
        lexer.LiteralLoss.Should().HaveFlag(LexerNs.LiteralLoss.AbortInsideLiteral, "the abort happened inside the read");
    }

    /// <summary>
    /// D19: an opener inserted ABOVE an existing closed multi-line literal of the same kind. The new
    /// opener pairs with the old opener's quotes, the old content lexes as code and the old closer opens
    /// a literal that runs to end of input. Control: the document without the inserted line is clean.
    /// </summary>
    [Theory]
    [MemberData(nameof(PrefixByTripleQuote))]
    public void OpenerInsertedAboveAClosedLiteral_SetsTheFact(string prefix, string quotes)
    {
        var closed = $"def g() -> str:\n    s = {prefix}{quotes}\n        key: value\n    {quotes}\n    return s\n";
        var control = Lex(closed);
        control.Diagnostics.HasErrors.Should().BeFalse(Errors(control));
        control.LiteralStateUnknown.Should().BeFalse();

        var lexer = Lex($"def f() -> int:\n    {prefix}{quotes}\n    return 1\n" + closed);

        lexer.Diagnostics.HasErrors.Should().BeTrue();
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
    }

    // ---------------------------------------------------------------- aborts inside a CLOSED literal

    /// <summary>D20f (and its t/df twins): a lone <c>}</c> aborts inside a literal whose closer is in the text.</summary>
    [Theory]
    [InlineData("f")]
    [InlineData("t")]
    [InlineData("df")]
    public void UnmatchedBraceInsideAClosedTripleFString_SetsTheFact(string prefix)
    {
        var lexer = Lex($"def main():\n    s = {prefix}\"\"\"\n        }}\n        key: value\n    \"\"\"\n    print(s)\n");

        Errors(lexer).Should().Contain("Unmatched '}'");
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
    }

    /// <summary>D20b: a non-ASCII character aborts inside a closed <c>b"""</c>.</summary>
    [Fact]
    public void NonAsciiInsideAClosedTripleByteString_SetsTheFact()
    {
        var lexer = Lex("def main():\n    s = b\"\"\"\n        \u00e9\n        key: value\n    \"\"\"\n    print(s)\n");

        Errors(lexer).Should().Contain("bytes can only contain ASCII");
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
    }

    /// <summary>An invalid escape aborts inside a closed plain <c>"""</c> (the escape reader runs inside the read).</summary>
    [Fact]
    public void InvalidEscapeInsideAClosedTripleString_SetsTheFact()
    {
        var lexer = Lex("def main():\n    s = \"\"\"\n        \\xZZ\n        key: value\n    \"\"\"\n    print(s)\n");

        Errors(lexer).Should().Contain("Invalid hex escape");
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
    }

    /// <summary>A single-quoted f-string nested in a triple f-string's field: the enclosing context is triple.</summary>
    [Fact]
    public void AbortInASingleQuotedFStringNestedInATripleOne_SetsTheFact()
    {
        var lexer = Lex("def main():\n    s = f\"\"\"\n        {f\"{x\"}\n        key: value\n    \"\"\"\n    print(s)\n");

        lexer.Diagnostics.HasErrors.Should().BeTrue();
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
    }

    // ---------------------------------------------------------------- single-quoted literals set nothing

    /// <summary>
    /// An unterminated single-quoted literal of every prefix ends at its line: nothing is set, so an
    /// unfinished <c>x = "abc</c> does not switch anything off. Positive control: its triple-quoted twin,
    /// cut the same way, sets the fact.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllPrefixes))]
    public void UnterminatedSingleQuotedLiteral_SetsNothing(string prefix)
    {
        var single = Lex($"x = {prefix}\"abc\ny = 1\n");
        single.Diagnostics.HasErrors.Should().BeTrue();
        single.LiteralStateUnknown.Should().BeFalse(Errors(single));

        var twin = Lex($"x = {prefix}\"\"\"abc\ny = 1\n");
        twin.Diagnostics.HasErrors.Should().BeTrue();
        twin.LiteralStateUnknown.Should().BeTrue(Errors(twin));
    }

    /// <summary>
    /// <c>x = f"{a</c> being typed: the unclosed field is reported at its bracket and recovery resumes on
    /// the next line (#2022) — the string began on that line, so nothing is set.
    /// </summary>
    [Fact]
    public void SingleQuotedFStringWithAnUnclosedFieldOnItsLine_SetsNothing()
    {
        var lexer = Lex("def main():\n    x = f\"{a\n    y = 1\n");

        Errors(lexer).Should().Contain("was never closed");
        lexer.LiteralStateUnknown.Should().BeFalse(Errors(lexer));
    }

    /// <summary>
    /// <c>fstrings/multiline_hole_2022</c> cut after <c>names</c>: the hole's innermost open bracket is
    /// the <c>(</c> of <c>join(</c>, on the string's start line, and the hole crossed a line break inside
    /// it before the source ended — the line after the bracket is the hole's, not code: set. Control,
    /// same prefix: the field's own <c>{</c> is the innermost bracket (the field being typed), nothing.
    /// </summary>
    [Theory]
    [InlineData("f")]
    [InlineData("t")]
    [InlineData("df")]
    public void SingleQuotedHoleThatCrossedALineInsideABracketOnItsStartLine_SetsTheFact(string prefix)
    {
        var lexer = Lex($"def main():\n    print({prefix}\"{{', '.join(\n        names\n");

        Errors(lexer).Should().Contain("'(' was never closed @2:");
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));

        var control = Lex($"def main():\n    x = {prefix}\"{{a\n    y = 1\n");
        Errors(control).Should().Contain("'{' was never closed @2:");
        control.LiteralStateUnknown.Should().BeFalse(Errors(control));
    }

    /// <summary>A single-quoted f-string whose field crossed a line break (PEP 701) before the abort: set.</summary>
    [Fact]
    public void SingleQuotedFStringThatCrossedALineBreak_SetsTheFact()
    {
        var lexer = Lex("def main():\n    s = f\"{a +\n        b} tail\n    y = 1\n");

        Errors(lexer).Should().Contain("Unterminated f-string");
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
    }

    /// <summary>An abort outside every literal sets nothing (the rest of its line, dropped, holds no literal either).</summary>
    [Fact]
    public void AbortOutsideAnyLiteral_SetsNothing()
    {
        var lexer = Lex("def main():\n    s = \"\"\"\n        key: value\n    \"\"\"\n    x = 1__2\n");

        Errors(lexer).Should().Contain("consecutive underscores");
        lexer.LiteralStateUnknown.Should().BeFalse(Errors(lexer));
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.None);
    }

    // ---------------------------------------------------------------- abort-free loss (P22f, #2271)
    //
    // Each mechanism loses a literal without any abort inside its read: the flag that names the mechanism
    // is the one set (Be, not HaveFlag — AbortInsideLiteral and the other two stay clear), over every
    // triple-quoted prefix and both quote characters.

    /// <summary>The leading whitespace that drops a line at an indentation error, and the code it raises.</summary>
    private static readonly (string Whitespace, string Code)[] IndentationErrors =
    {
        ("  ", "SPY0013"),     // width not a multiple of 4
        ("\t", "SPY0012"),     // a tab
        ("\t ", "SPY0011"),    // mixed tabs and spaces
    };

    public static IEnumerable<object[]> PrefixByTripleQuoteByIndentationError()
        => PrefixByTripleQuote().SelectMany(row => IndentationErrors.Select(e => new object[] { row[0], row[1], e.Whitespace, e.Code }));

    private static readonly string[] OrphanPlacements = { "adjacent", "spaced", "comment" };

    public static IEnumerable<object[]> PrefixByTripleQuoteByOrphan()
        => PrefixByTripleQuote().SelectMany(row => OrphanPlacements.Select(p => new object[] { row[0], row[1], p }));

    /// <summary>
    /// The short string that re-pairs a triple: <c>'"""'</c> (adjacent — the closer is followed by a quote,
    /// arm a), <c>'""" '</c> (spaced — an unterminated short string on the close line, arm b),
    /// <c>'"""'  # don't</c> (comment — the orphan quote pairs with the comment's apostrophe: only arm a
    /// sees it). The orphan quote is the other quote character.
    /// </summary>
    private static string Orphan(string quotes, string placement)
    {
        var o = quotes[0] == '"' ? '\'' : '"';
        return placement switch
        {
            "adjacent" => $"{o}{quotes}{o}",
            "spaced" => $"{o}{quotes} {o}",
            "comment" => $"{o}{quotes}{o}  # don{o}t",
            _ => throw new ArgumentOutOfRangeException(nameof(placement)),
        };
    }

    /// <summary>
    /// N4 (and its SPY0012/SPY0011 siblings): the opener and closer lines of a literal are dropped whole at an
    /// indentation error (the abort happens before any token of the line is read), so the pair is never read
    /// and the content lexes as code. Positive twin of the N4q/N4t/X4 controls below.
    /// </summary>
    [Theory]
    [MemberData(nameof(PrefixByTripleQuoteByIndentationError))]
    public void OpenerLineDroppedAtAnIndentationError_SetsDroppedOpener(string prefix, string quotes, string whitespace, string code)
    {
        var lexer = Lex($"def main():\n{whitespace}s = {prefix}{quotes}\n        key: value\n{whitespace}{quotes}\n{whitespace}print(s)\n");

        Errors(lexer).Should().Contain($"{code} ");
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.DroppedOpener, Errors(lexer));
    }

    /// <summary>N4m: a wide body, the delimiter lines at width 4 under it (SPY0014 — on no enclosing level).</summary>
    [Theory]
    [MemberData(nameof(PrefixByTripleQuote))]
    public void OpenerLineDroppedAtAnIndentationMismatch_SetsDroppedOpener(string prefix, string quotes)
    {
        var lexer = Lex($"def main():\n        x = 1\n    s = {prefix}{quotes}\n        key: value\n    {quotes}\n    print(s)\n");

        Errors(lexer).Should().Contain("SPY0014 ");
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.DroppedOpener, Errors(lexer));
    }

    /// <summary>
    /// X6: an unexpected character before the opener on a valid-width line drops the rest of the line,
    /// opener included (the closer line then drops at SPY0014 under the content's width).
    /// </summary>
    [Theory]
    [MemberData(nameof(PrefixByTripleQuote))]
    public void OpenerLineDroppedAtAMidLineUnexpectedCharacter_SetsDroppedOpener(string prefix, string quotes)
    {
        var lexer = Lex($"def main():\n    x = ${prefix}{quotes}\n        key: value\n    {quotes}\n    print(x)\n");

        Errors(lexer).Should().Contain("SPY0015 ");
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.DroppedOpener, Errors(lexer));
    }

    /// <summary>The short strings that abort mid-line with the lexer INSIDE them, and the code each raises.</summary>
    private static readonly (string ShortString, string Code)[] MidStringAborts =
    {
        ("\"\\q\"", "SPY0004"),         // an invalid escape
        ("\"\\x4\"", "SPY0005"),        // a hex escape short of two digits
        ("\"\\u12\"", "SPY0006"),       // a unicode escape short of four digits
        ("b\"\u00e9\"", "SPY0028"),     // a non-ASCII character in a b string
        ("f\"a}b\"", "SPY0021"),        // a lone '}' in an f-string's text
    };

    public static IEnumerable<object[]> PrefixByTripleQuoteByMidStringAbort()
        => PrefixByTripleQuote().SelectMany(row => MidStringAborts.Select(a => new object[] { row[0], row[1], a.ShortString, a.Code }));

    /// <summary>
    /// A short string before the opener aborts mid-line with the lexer inside it; recovery drops the rest of
    /// the line, opener included — and the same on the closer's line, so the triple count stays even and the
    /// lex is abort-free (the verifier's C1 cell, @ 4b7428567 the full fallback rewrote <c>    key: value</c>).
    /// The dropped span starts at the aborted string's prefix, not inside it, where its quotes would pair
    /// the wrong way.
    /// </summary>
    [Theory]
    [MemberData(nameof(PrefixByTripleQuoteByMidStringAbort))]
    public void OpenerLineDroppedMidStringAbort_SetsDroppedOpener(string prefix, string quotes, string shortString, string code)
    {
        var lexer = Lex($"x = {shortString} + {prefix}{quotes}\n    key: value\n{shortString} + {quotes}\nprint(x)\n");

        Errors(lexer).Should().Contain($"{code} ");
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.DroppedOpener, Errors(lexer));
    }

    /// <summary>
    /// A string nested in a replacement field, on a dropped line (the second verification of P22f @
    /// 37e5c1d02): the dropped span is read the way the lexer reads a hole. E1par2/E1par (a nested quote)
    /// and L4close4 (a nested <c>}</c>) hold a literal that spans lines; W12 (a nested <c>{</c>), M17 (the
    /// same, after a mid-line abort) and M16 (a closed nested triple) hold none, each beside a positive twin
    /// on the same shape (the field left open, or an opener after the f-string).
    /// </summary>
    [Theory]
    [InlineData("E1par2", "def main():\n    x = f\"{'\"'}\\q\" + \"\"\"\n        key: value\n        f\"{'\"'}\\q\" + \"\"\"\n    print(x)\n", "SPY0004", LexerNs.LiteralLoss.DroppedOpener)]
    [InlineData("E1par", "def main():\n    x = f\"{\"\\\"\"}\\q\" + \"\"\"\n        key: value\n        f\"{\"\\\"\"}\\q\" + \"\"\"\n    print(x)\n", "SPY0004", LexerNs.LiteralLoss.DroppedOpener)]
    [InlineData("L4close4", "def main():\n    y = \"\\q\" + f\"{d['}'] +\n            x}\"  # \"\n    print(y)\n", "SPY0004", LexerNs.LiteralLoss.DroppedOpener)]
    [InlineData("W12", "def main():\n  x: int = 1\n  y = f\"{'{'}\n  print(x)\n", "SPY0013", LexerNs.LiteralLoss.None)]
    [InlineData("W12, the field left open", "def main():\n  x: int = 1\n  y = f\"{'}'\n  print(x)\n", "SPY0013", LexerNs.LiteralLoss.DroppedOpener)]
    [InlineData("M17", "y = \"\\q\" + f\"{'{'}\\q\ndef main():\n  x: int = 1\n  print(x)\n", "SPY0004", LexerNs.LiteralLoss.None)]
    [InlineData("M17nestbrace", "y = f\"{'{'}\\q\ndef main():\n  x: int = 1\n  print(x)\n", "SPY0004", LexerNs.LiteralLoss.None)]
    [InlineData("M17, the field left open", "y = \"\\q\" + f\"{'}'\\q\ndef main():\n  x: int = 1\n  print(x)\n", "SPY0004", LexerNs.LiteralLoss.DroppedOpener)]
    [InlineData("M16", "y = f\"{\"\"\"a\"\"\"}\\q\" + 'z'\ndef main():\n  x: int = 1\n  print(x)\n", "SPY0004", LexerNs.LiteralLoss.None)]
    [InlineData("M16, an opener after it", "y = f\"{\"\"\"a\"\"\"}\\q\" + \"\"\"z\ndef main():\n  x: int = 1\n  print(x)\n", "SPY0004", LexerNs.LiteralLoss.DroppedOpener)]
    public void StringNestedInAReplacementField_OnADroppedLine(string name, string source, string code, LexerNs.LiteralLoss expected)
    {
        var lexer = Lex(source);

        Errors(lexer).Should().Contain($"{code} ", name);
        lexer.LiteralLoss.Should().Be(expected, "{0}: {1}", name, Errors(lexer));
    }

    /// <summary>
    /// A backtick-delimited name on a dropped line is opaque outside a literal as well as inside a replacement
    /// field (wave-5 sibling @ 390d701d7): read flat, the apostrophe in <c>`it's`</c> opened a short string that
    /// swallowed the opener after it. The control keeps the same line with no opener after the name.
    /// </summary>
    [Theory]
    [InlineData("x = $`it's` + \"\"\"\n    key: value\n$`it's` + \"\"\"\nprint(x)\n", LexerNs.LiteralLoss.DroppedOpener)]
    [InlineData("x = $`a#b` + \"\"\"\n    key: value\n$`a#b` + \"\"\"\nprint(x)\n", LexerNs.LiteralLoss.DroppedOpener)]
    [InlineData("x = $`it's` + 'z'\ndef main():\n  x: int = 1\n  print(x)\n", LexerNs.LiteralLoss.None)]
    public void BacktickNameOnADroppedLine_IsOpaque(string source, LexerNs.LiteralLoss expected)
    {
        var lexer = Lex(source);

        Errors(lexer).Should().Contain("SPY0015 ");
        lexer.LiteralLoss.Should().Be(expected, Errors(lexer));
    }

    /// <summary>
    /// The two conditions under which the dropped span is read from the start of the token being read
    /// (<c>_tokenStart</c>) instead of from the abort. (a) No line break between them: an abort on a later
    /// line of a literal that spans lines reads from the abort, so that literal's own opener is not a dropped
    /// one (its loss is <see cref="LexerNs.LiteralLoss.AbortInsideLiteral"/> alone) — without (a) the first
    /// two rows also set DroppedOpener. (b) The token is a string literal: a backtick-delimited name holding a
    /// triple opens nothing — without (b) the third row sets DroppedOpener. The fourth row is its positive
    /// twin: a string token reads from its start.
    /// </summary>
    [Theory]
    [InlineData("s = \"\"\"\n\\xZZ\n\"\"\"\nprint(s)\n", "SPY0005", LexerNs.LiteralLoss.AbortInsideLiteral)]
    [InlineData("def main():\n    x = f\"{a +\n        b}\\q\"\n    print(x)\n", "SPY0004", LexerNs.LiteralLoss.AbortInsideLiteral)]
    [InlineData("def main():\n    x = `a\"\"\"b\n    print(x)\n", "SPY0018", LexerNs.LiteralLoss.None)]
    [InlineData("def main():\n    x = \"\\q\" + \"\"\"b\n    print(x)\n", "SPY0004", LexerNs.LiteralLoss.DroppedOpener)]
    public void DroppedSpanReadFromTheTokenStart_OnlyForAStringBegunOnTheAbortsLine(string source, string code, LexerNs.LiteralLoss expected)
    {
        var lexer = Lex(source);

        Errors(lexer).Should().Contain($"{code} ");
        lexer.LiteralLoss.Should().Be(expected, Errors(lexer));
    }

    /// <summary>N4f: a single-quoted f-/t-string whose replacement field continues on the next line, on a dropped line.</summary>
    [Theory]
    [InlineData("f")]
    [InlineData("t")]
    [InlineData("df")]
    public void SingleQuotedHoleOpenedOnADroppedLine_SetsDroppedOpener(string prefix)
    {
        var lexer = Lex($"def main():\n  x = {prefix}\"{{', '.join(\n        names\n      )}}\"\n  print(x)\n");

        Errors(lexer).Should().Contain("SPY0013 ");
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.DroppedOpener, Errors(lexer));
    }

    /// <summary>
    /// N1: the error budget stops the lexer above a CLOSED literal; the rest of the source gets no token, so
    /// its interior lines read as dropped code lines. The 25 unterminated short strings above it each abort
    /// on their line break and drop nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(PrefixByTripleQuote))]
    public void BudgetStopWithALiteralLeft_SetsUnreadRemainder(string prefix, string quotes)
    {
        var maxErrors = new LexerNs.Lexer("").MaxErrors;
        var lexer = Lex(string.Concat(Enumerable.Repeat("x = \"abc\n", maxErrors))
            + $"def main():\n    s = {prefix}{quotes}\n        key: value\n    {quotes}\n    print(s)\n");

        lexer.Diagnostics.ErrorCount.Should().Be(maxErrors);
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.UnreadRemainder, Errors(lexer));
    }

    /// <summary>
    /// N2 (adjacent), X8 (spaced), N2c (comment), and N2w (the quote characters swapped): a stray triple
    /// above a closed literal pairs with its opener; the literal's closer then opens a run that a short
    /// string holding the same triple closes mid-line. The triple count is even, so no read aborts.
    /// </summary>
    [Theory]
    [MemberData(nameof(PrefixByTripleQuoteByOrphan))]
    public void ClosedLiteralRePairedIntoAShortString_SetsRePairedCloser(string prefix, string quotes, string placement)
    {
        var lexer = Lex($"{quotes}\ndef main():\n    s = {prefix}{quotes}\n      key: value\n    {quotes}\n    t = {Orphan(quotes, placement)}\n    print(s, t)\n");

        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.RePairedCloser, Errors(lexer));
    }

    /// <summary>
    /// The prefixed twin of the re-pair: in the N2 shape the run that closes inside the short string is
    /// always opened by the literal's BARE closer line, so it reaches only the plain close arm. Here the
    /// prefixed opener's own run closes there — every one of the five close arms (plain and <c>d</c>,
    /// <c>b</c>, <c>r</c>, <c>dr</c>, <c>f</c>/<c>t</c>/<c>df</c>) records the closer's context.
    /// </summary>
    [Theory]
    [MemberData(nameof(PrefixByTripleQuoteByOrphan))]
    public void PrefixedOpenerRePairedIntoAShortString_SetsRePairedCloser(string prefix, string quotes, string placement)
    {
        var lexer = Lex($"def main():\n    s = {prefix}{quotes}\n        key: value\n    t = {Orphan(quotes, placement)}\n    print(s, t)\n");

        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.RePairedCloser, Errors(lexer));
    }

    // ---------------------------------------------------------------- the direction controls: nothing lost, nothing set

    /// <summary>
    /// Dropped or re-paired text that opens nothing multi-line keeps the fact clear, so the indent-only
    /// routes keep their repairs: TWO (the owner's 2-space document), N4q (a dropped line holding a short
    /// string), N4t (a short string holding a triple), X4 (a triple closed on its own dropped line), and a
    /// triple run closed on its own line followed by an unterminated short string (the RULED (b)
    /// restriction — a single-line run cannot have lost multi-line content) or by a quote (arm (a), the same
    /// restriction), an unterminated f-string whose replacement fields are closed (only an OPEN field can
    /// span lines), and a short string aborting mid-line before a triple closed on its line. Each lexes with
    /// errors (the hooks run); the theories above are their positive twins.
    /// </summary>
    [Theory]
    [InlineData("def main():\n  x: int = 1\n  print(x)\n")]
    [InlineData("def main():\n  x = \"hi\"\n  print(x)\n")]
    [InlineData("def main():\n  t = '\"\"\"'\n  print(t)\n")]
    [InlineData("def main():\n  s = \"\"\"abc\"\"\"\n  print(s)\n")]
    [InlineData("x = \"\"\"a\"\"\" + 'b\n")]
    // an unterminated f-string with no OPEN replacement field, on a dropped line: it ends at its line
    [InlineData("def main():\n  x: int = 1\n  print(f\"total: {x}\n  print(x)\n")]
    [InlineData("def main():\n  x: int = 1\n  y = f\"hello\n  print(x)\n")]
    // a triple run closed on the line it opened on, its closer followed by a quote (arm a's single-line control)
    [InlineData("z = \"\"\"a\"\"\"\"\ndef main():\n  x: int = 1\n  print(x)\n")]
    [InlineData("z = \"\"\"a\"\"\"'b\ndef main():\n  x: int = 1\n  print(x)\n")]
    [InlineData("z = \"\"\"\"\"\"\"\ndef main():\n  x: int = 1\n  print(x)\n")]
    // a mid-line abort inside a short string whose dropped rest of the line holds a triple closed on it
    [InlineData("def main():\n    x = \"\\q\" + \"\"\"abc\"\"\"\n    print(x)\n")]
    public void DroppedOrRePairedTextThatOpensNothingMultiLine_SetsNothing(string source)
    {
        var lexer = Lex(source);

        lexer.Diagnostics.HasErrors.Should().BeTrue();
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.None, Errors(lexer));
    }

    /// <summary>The RULED (b) control's multi-line twin (N2typ): the same keystroke on a run that spanned lines sets it.</summary>
    [Fact]
    public void UnterminatedShortStringOnTheCloseLineOfARunThatSpannedLines_SetsRePairedCloser()
    {
        var lexer = Lex("def main():\n    s = \"\"\"abc\n    def\"\"\" + \"typing\n        x = 1\n");

        Errors(lexer).Should().Contain("SPY0001 ");
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.RePairedCloser, Errors(lexer));
    }

    // ---------------------------------------------------------------- P22g (#2275, R-FR): any abort on the close line

    public static IEnumerable<object[]> CloseLineCodeByTripleQuote()
        => FormatterTwins.CloseLineCodes.SelectMany(c => new[] { "\"\"\"", "'''" }.Select(q => new object[] { c.Code, q }));

    /// <summary>The close line of the N2 shape, line 6: a stray triple, a closed block, and the orphan line.</summary>
    private static string N2Shape(string quotes, string orphanLine)
        => $"{quotes}\ndef main():\n    s = {quotes}\n      key: value\n    {quotes}\n    {orphanLine}\n    print(s)\n";

    private static string?[] CodesOnLine(LexerNs.Lexer lexer, int line)
        => lexer.Diagnostics.GetErrors().Where(d => d.Line == line).Select(d => d.Code).ToArray();

    /// <summary>
    /// #2275: the N2 shape whose orphan line aborts right after the re-paired closer, at each code of
    /// <see cref="FormatterTwins.CloseLineCodes"/> (the recipe's own tails) — the orphan's closing quote sits in the
    /// text the lexer gives up, so arm (b) records <c>RePairedCloser</c> whatever the code (R-FR). The close line's
    /// code is asserted first: a row that reports another code is a test defect. Before P22g only the four
    /// unterminated-string codes reached arm (b).
    /// </summary>
    [Theory]
    [MemberData(nameof(CloseLineCodeByTripleQuote))]
    public void AbortOnTheCloseLineWithAQuoteInTheDroppedSpan_SetsRePairedCloser(string code, string quotes)
    {
        var o = quotes[0] == '"' ? '\'' : '"';
        var tail = FormatterTwins.CloseLineCodes.Single(c => c.Code == code).OrphanTailFor(quotes[0]);
        var lexer = Lex(N2Shape(quotes, $"t = {o}{quotes}{tail}"));

        CodesOnLine(lexer, 6).Should().Equal(new[] { code }, Errors(lexer));
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.RePairedCloser, Errors(lexer));
    }

    /// <summary>
    /// #2275's other orphan spellings: the dropped span after the abort holds the orphan's quote — after the abort
    /// (<c>$5'</c>, <c> $ '</c>), and inside a backtick-delimited name the lexer reads to its line end before it
    /// reports SPY0018 (<c>`'</c>, <c> see `x'</c>, <c>`x'</c>): the span starts at the name (UnreadTextStart), else it
    /// would be empty and the quote never seen.
    /// </summary>
    [Theory]
    [InlineData("t = '\"\"\" costs $5'", "SPY0015")]
    [InlineData("t = '\"\"\" $ '", "SPY0015")]
    [InlineData("t = '\"\"\"`'", "SPY0018")]
    [InlineData("t = '\"\"\" see `x'", "SPY0018")]
    [InlineData("t = '\"\"\"`x'", "SPY0018")]
    public void OrphanQuoteInTheTextTheLexerGivesUp_SetsRePairedCloser(string orphanLine, string code)
    {
        var lexer = Lex(N2Shape("\"\"\"", orphanLine));

        CodesOnLine(lexer, 6).Should().Equal(new[] { code }, Errors(lexer));
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.RePairedCloser, Errors(lexer));
    }

    /// <summary>
    /// The known limit of #2274 that R-FR (b) cannot reach (R-btname): the orphan quote is READ — it opens the short
    /// string <c>' + `it'</c>, closed by the apostrophe inside a later backtick name — and the aborting backtick's
    /// text holds no quote: the lexer signature of <c>""" + `x</c> on a close line, which loses nothing. Pinned so a
    /// later cure is a visible change.
    /// </summary>
    [Fact]
    public void OrphanQuoteReadAsAShortString_RBtname_SetsNothing_KnownLimit2274()
    {
        var lexer = Lex(N2Shape("\"\"\"", "t = '\"\"\"x' + `it's`"));

        CodesOnLine(lexer, 6).Should().Equal(new[] { "SPY0018" }, Errors(lexer));
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.None, Errors(lexer));
    }

    /// <summary>
    /// #2275's direction control: no stray, a closer line that aborts and drops no quote (<c>""" + 1__2</c>,
    /// <c>""" + $</c>, a backtick name, <c>0x</c>, an f-string spec) lost nothing and records nothing. With the
    /// quote condition dropped (R-FR's rejected option (a)) these go red.
    /// </summary>
    [Theory]
    [MemberData(nameof(CloseLineCodeByTripleQuote))]
    public void AbortOnTheCloseLineWithNoQuote_SetsNothing(string code, string quotes)
    {
        var tail = FormatterTwins.CloseLineCodes.Single(c => c.Code == code).ControlTail;
        var lexer = Lex($"def main():\n    s = {quotes}\n      a\n    {quotes} + {tail}\n    print(s)\n");

        CodesOnLine(lexer, 4).Should().Equal(new[] { code }, Errors(lexer));
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.None, Errors(lexer));
    }

    /// <summary>
    /// The budget stop landing ON the close line: 23 errors above the N2 shape, whose <c>key: value</c> line is the
    /// 24th (SPY0013) and the orphan's abort the 25th. The stop runs no recovery, so it reads the close-line rule
    /// itself (one NoteUnreadLine for every hook).
    /// </summary>
    [Fact]
    public void BudgetStopOnTheCloseLine_SetsRePairedCloser()
    {
        var lexer = Lex(string.Concat(Enumerable.Repeat("x = \"abc\n", 23)) + N2Shape("\"\"\"", "t = '\"\"\"$'"));

        lexer.Diagnostics.GetWarnings().Should().Contain(d => d.Code == "SPY0905", "the 25th error stops the lexer");
        CodesOnLine(lexer, 23 + 6).Should().Equal(new[] { "SPY0015" }, Errors(lexer));
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.RePairedCloser, Errors(lexer));
    }

    /// <summary>
    /// The end-of-source stop on the close line: the orphan line is the document's last, with no line break, so the
    /// unterminated short string (SPY0001) aborts at the end of the source and no recovery runs — the stop reads the
    /// close-line rule itself, as the budget stop does (the four-code arm in ReportError used to cover it).
    /// </summary>
    [Theory]
    [InlineData("t = '\"\"\" '")]
    [InlineData("t = '\"\"\" f'{x}")]
    public void AbortAtTheEndOfTheSourceOnTheCloseLine_SetsRePairedCloser(string orphanLine)
    {
        var lexer = Lex(N2Shape("\"\"\"", orphanLine).Replace("\n    print(s)\n", "", StringComparison.Ordinal));

        lexer.Diagnostics.GetErrors().Should().Contain(d => d.Line == 6, Errors(lexer));
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.RePairedCloser, Errors(lexer));
    }

    // ---------------------------------------------------------------- P22h (#2280, R-FV): the close-line walk

    public static IEnumerable<object[]> OwnQuoteTailByTripleQuote()
        => FormatterTwins.OwnQuoteTails.SelectMany(t => new[] { "\"\"\"", "'''" }.Select(q => new object[] { t.Name, q }));

    private static FormatterTwins.OwnQuoteTail OwnQuoteTail(string name) => FormatterTwins.OwnQuoteTails.Single(t => t.Name == name);

    /// <summary>
    /// #2280 (R-FV): NO stray — a closer line ending in a literal that aborts INSIDE itself (<c>""" + "\q"</c>,
    /// <c>""" + f"{x!q}"</c>, <c>""" + f"}"</c>, …) lost nothing. Arm (b) reads the quote's ROLE from a walk of the
    /// given-up text (<c>LeavesAQuoteUnpairedAtLineEnd</c>): the aborted literal's own delimiters pair, so the six
    /// pairing tails record nothing (before R-FV any quote counted: <c>RePairedCloser</c>, and every route lost the
    /// repair). The three tails the walk cannot pair — <c>`it's</c> (an unterminated backtick name holding a quote),
    /// <c>f"{x:">}"</c> (the own quote in a spec: "either walk", #2276), <c>$  # it's</c> (a quote after <c>#</c>) —
    /// keep <c>RePairedCloser</c>: the known limits of #2274 (lead ruling L2), pinned by name; their stray-closed twins
    /// in <see cref="CloseLineOwnQuoteTail_StrayClosed_SetsRePairedCloser"/> are why. The close line must abort.
    /// </summary>
    [Theory]
    [MemberData(nameof(OwnQuoteTailByTripleQuote))]
    public void CloseLineOwnQuoteTail_NoStray_SetsNothing_ExceptTheThreeLimits(string tail, string quotes)
    {
        var own = OwnQuoteTail(tail);
        var lexer = Lex($"def main():\n    s = {quotes}\n      a\n    {quotes} + {own.TailFor(quotes[0])}\n    print(s)\n");

        CodesOnLine(lexer, 4).Should().NotBeEmpty("the tail must abort on the close line");
        lexer.LiteralLoss.Should().Be(own.WalkCannotPair ? LexerNs.LiteralLoss.RePairedCloser : LexerNs.LiteralLoss.None,
            $"{tail}: {(own.WalkCannotPair ? "a known limit of #2274 — the walk cannot pair its quote" : "the aborted literal's own delimiters pair")} ({Errors(lexer)})");
    }

    /// <summary>
    /// R-FV's positive control for every new tail: the N2 shape whose orphan is CLOSED after the tail
    /// (<c>_ = '""" + &lt;tail&gt;'</c> below a stray triple) — the closer re-paired with the orphan, the orphan's
    /// closing quote is in the text the lexer gives up AFTER the tail's own delimiters, so it is unpaired at the line's
    /// end (for <c>`it's'</c> and <c>$  # it's'</c> inside text the walk cannot pair) — <c>RePairedCloser</c> on all
    /// nine. A walk that "repairs" a limit tail by pairing more (reading <c>#</c> as text or as a comment, an
    /// unterminated backtick name as opaque) loses one of these.
    /// </summary>
    [Theory]
    [MemberData(nameof(OwnQuoteTailByTripleQuote))]
    public void CloseLineOwnQuoteTail_StrayClosed_SetsRePairedCloser(string tail, string quotes)
    {
        var o = quotes[0] == '"' ? '\'' : '"';
        var lexer = Lex(N2Shape(quotes, $"_ = {o}{quotes} + {OwnQuoteTail(tail).TailFor(quotes[0])}{o}"));

        CodesOnLine(lexer, 6).Should().NotBeEmpty("the tail must abort on the re-paired close line");
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.RePairedCloser, $"{tail} ({Errors(lexer)})");
    }

    /// <summary>
    /// The orphan still being typed (<c>_ = '""" + &lt;tail&gt;</c>, its closing quote not yet typed, below a stray
    /// triple): once the tail's own delimiters pair, the given-up text leaves no quote unpaired — the lexer signature of
    /// the no-stray tail, which loses nothing. Pinned as #2274's class (R-FQ: a shape with no lexer signature but the
    /// stray), the accepted trade of R-FV (a): the six pairing tails record nothing; the three the walk cannot pair still
    /// record <c>RePairedCloser</c>. The quote-free <c>$</c> row is the class's pre-existing member.
    /// </summary>
    [Theory]
    [MemberData(nameof(OwnQuoteTailByTripleQuote))]
    public void OrphanBeingTypedBeforeAnOwnQuoteTail_KnownLimit2274(string tail, string quotes)
    {
        var o = quotes[0] == '"' ? '\'' : '"';
        var own = OwnQuoteTail(tail);
        var lexer = Lex(N2Shape(quotes, $"_ = {o}{quotes} + {own.TailFor(quotes[0])}"));

        CodesOnLine(lexer, 6).Should().NotBeEmpty("the tail must abort on the re-paired close line");
        lexer.LiteralLoss.Should().Be(own.WalkCannotPair ? LexerNs.LiteralLoss.RePairedCloser : LexerNs.LiteralLoss.None, $"{tail} ({Errors(lexer)})");
    }

    /// <summary>
    /// The quote-free member of the typing-orphan class (#2274): <c>_ = '""" + $</c> below a stray triple has the
    /// lexer signature of <c>""" + $</c> with no stray. Measured @ be1c16fcf: full / range-line 3 / on-type 3 rewrite the
    /// string line <c>      key: value</c> — a pre-existing #2274-class wrong edit that R-FV neither causes nor cures.
    /// </summary>
    [Theory]
    [InlineData("\"\"\"")]
    [InlineData("'''")]
    public void OrphanBeingTypedBeforeAnAbortWithNoQuote_SetsNothing_KnownLimit2274(string quotes)
    {
        var o = quotes[0] == '"' ? '\'' : '"';
        var lexer = Lex(N2Shape(quotes, $"_ = {o}{quotes} + $"));

        CodesOnLine(lexer, 6).Should().Equal(new[] { "SPY0015" }, Errors(lexer));
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.None, Errors(lexer));
    }

    // ---------------------------------------------------------------- P22g (#2276): the spec-quote abort

    /// <summary>
    /// #2276 through the real lexer: at the literal's own quote in a format spec the lexer aborts (SPY0022) and drops
    /// the rest of the line — here an opener — so the dropped span (read from the f-string's prefix) holds a literal
    /// spanning lines: <c>DroppedOpener</c>. SQ2 (in a block) and SQ3 (module level, the closer line at column 0);
    /// no spec text before the quote; the t-string twin. @ 926670989 the fact was None.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    x = f\"{x:\">}\" + \"\"\"\n        key: value\n        f\"{x:\">}\" + \"\"\"\n    print(x)\n")]
    [InlineData("x = f\"{x:\">}\" + \"\"\"\n    key: value\nf\"{x:\">}\" + \"\"\"\nprint(x)\n")]
    [InlineData("def main():\n    x = f\"{x:\" + \"\"\"\n        key: value\n        \"\"\"\n    print(x)\n")]
    [InlineData("def main():\n    x = t\"{x:\">}\" + \"\"\"\n        key: value\n        t\"{x:\">}\" + \"\"\"\n    print(x)\n")]
    public void SpecQuoteAbortBeforeAnOpener_SetsDroppedOpener(string source)
    {
        var lexer = Lex(source);

        lexer.Diagnostics.GetErrors().First().Code.Should().Be("SPY0022", Errors(lexer));
        lexer.LiteralLoss.Should().HaveFlag(LexerNs.LiteralLoss.DroppedOpener, Errors(lexer));
    }

    /// <summary>
    /// The spec-quote abort's direction controls: a spec being typed (<c>f"{x:"</c>) and a spec whose quote is
    /// followed by no opener lose nothing and record nothing — on-type keeps working while a spec is typed.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    x = f\"{x:\"\n    y = 1\n")]
    [InlineData("def main():\n    x = f\"{x:\">}\"\n    y = 1\n")]
    [InlineData("def main():\n    x = f\"{x:\">}\" + 'z'\n    y = 1\n")]
    public void SpecQuoteAbortWithNoOpener_SetsNothing(string source)
    {
        var lexer = Lex(source);

        lexer.Diagnostics.GetErrors().First().Code.Should().Be("SPY0022", Errors(lexer));
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.None, Errors(lexer));
    }

    /// <summary>
    /// #2276's sibling aborts through the real lexer (controls — the scanner already agreed): an unmatched <c>}</c>
    /// (SPY0021) and an invalid conversion (SPY0030) before an opener set <c>DroppedOpener</c>.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    x = f\"}\" + \"\"\"\n        key\n        \"\"\"\n    print(x)\n", "SPY0021")]
    [InlineData("def main():\n    x = f\"{x!q}\" + \"\"\"\n        key\n        \"\"\"\n    print(x)\n", "SPY0030")]
    public void OtherMidLiteralAbortBeforeAnOpener_SetsDroppedOpener(string source, string code)
    {
        var lexer = Lex(source);

        lexer.Diagnostics.GetErrors().First().Code.Should().Be(code, Errors(lexer));
        lexer.LiteralLoss.Should().HaveFlag(LexerNs.LiteralLoss.DroppedOpener, Errors(lexer));
    }

    /// <summary>
    /// A report that drops no text records nothing (R-FR: "a code that reports without dropping loses nothing"):
    /// the two dedented-string indentation errors are reported without an abort, on a literal that spans lines.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    s = d\"\"\"\n        a\n      b\n        \"\"\"\n    print(s)\n")]
    [InlineData("def main():\n    s = d\"\"\"\n        a\"\"\"\n    print(s)\n")]
    public void DedentedStringReport_DropsNothing_SetsNothing(string source)
    {
        var lexer = Lex(source);

        lexer.Diagnostics.GetErrors().Should().OnlyContain(d => d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.Lexer.DedentedStringIndentationError, Errors(lexer));
        lexer.Diagnostics.HasErrors.Should().BeTrue();
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.None, Errors(lexer));
    }

    /// <summary>
    /// N1c / N1s: a budget stop whose remainder holds no literal that spans lines — the fallback's main use
    /// case (a 2-space body) keeps its repair. Positive twin: <see cref="BudgetStopWithALiteralLeft_SetsUnreadRemainder"/>.
    /// </summary>
    [Theory]
    [InlineData("  x = 1\n", "")]
    [InlineData("  print(\"hi\")\n", "")]
    [InlineData("  x = 1\n", "  y = f\"x\n")]     // an unterminated f-string with no open field ends at its line
    public void BudgetStopWithNoLiteralLeft_SetsNothing(string bodyLine, string lastLine)
    {
        var lexer = Lex("def main():\n" + string.Concat(Enumerable.Repeat(bodyLine, 30)) + lastLine);

        lexer.Diagnostics.GetWarnings().Should().Contain(d => d.Code == "SPY0905", "the budget stopped the lexer with source left");
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.None, Errors(lexer));
    }

    /// <summary>
    /// The budget stop landing INSIDE an aborted short string (verify round @ 5c8d81f28, B1 / BS-esc): the
    /// remainder is read from that literal's own start, as a dropped span is (<c>UnreadTextStart</c>) —
    /// read from inside <c>"\q"</c>, <c>" + """abc"""</c> pairs as <c>" + "</c> then an opener. A triple
    /// closed on its line sets nothing (the 2-space body below keeps its repair); an opener left unread,
    /// after a plain, f- or byte-string abort, sets <c>UnreadRemainder</c>.
    /// </summary>
    [Theory]
    [InlineData("y = \"\\q\" + \"\"\"abc\"\"\"\n", LexerNs.LiteralLoss.None)]
    [InlineData("y = \"\\q\" + \"\"\"\n    key: value\n    \"\"\" + '\"\"\"'\n", LexerNs.LiteralLoss.UnreadRemainder)]
    [InlineData("y = f\"{a $ b}\" + \"\"\"\n    key: value\n    \"\"\" + '\"\"\"'\n", LexerNs.LiteralLoss.UnreadRemainder)]
    [InlineData("y = b\"\u00e9\" + \"\"\"\n    key: value\n    \"\"\" + '\"\"\"'\n", LexerNs.LiteralLoss.UnreadRemainder)]
    public void BudgetStopInsideAnAbortedShortString_ReadsTheRemainderFromTheLiteral(string twentyFifthErrorLine, LexerNs.LiteralLoss expected)
    {
        var maxErrors = new LexerNs.Lexer("").MaxErrors;
        var lexer = Lex(string.Concat(Enumerable.Repeat("x = \"abc\n", maxErrors - 1)) + twentyFifthErrorLine + "def main():\n  x = 1\n");

        lexer.Diagnostics.GetWarnings().Should().Contain(d => d.Code == "SPY0905", "the budget stopped the lexer with source left");
        lexer.LiteralLoss.Should().Be(expected, Errors(lexer));
    }

    /// <summary>
    /// <c>strings/d_string_trailing_newline</c> with <c>"""</c> inserted as line 0 (FormattingFallbackTests'
    /// <c>DStringRepaired</c>): it re-pairs into a comment, every closer is followed by a line break and the
    /// lex reports nothing — no lexer fact separates it from the known-limit cells of #2271, and it loses nothing.
    /// </summary>
    [Fact]
    public void TriplesRePairedThroughAComment_SetNothing()
    {
        var lexer = Lex("\"\"\"\ndef main():\n    # A blank line before the closing \"\"\" becomes a trailing newline\n    msg: str = d\"\"\"\n        hello\n\n        \"\"\"\n    print(repr(msg))\n");

        lexer.Diagnostics.HasErrors.Should().BeFalse(Errors(lexer));
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.None);
    }

    // ---------------------------------------------------------------- the corpus

    /// <summary>
    /// Diagnostic-neutral on the corpus: every <c>.spy</c> under the compiler's <c>TestFixtures/</c> that
    /// lexes clean has the fact false. Positive control, generated from the same corpus: each of its
    /// triple-quoted literals with a complete interior line, cut after that line, sets it.
    /// </summary>
    [Fact]
    public void Corpus_CleanFixturesSetNothing_AndEveryCutMultiLineLiteralSetsTheFact()
    {
        var root = FixtureRoots.CompilerTests.Path;
        var files = Directory.EnumerateFiles(root, "*.spy", SearchOption.AllDirectories)
            .Where(f => !Sharpy.Compiler.Diagnostics.CrashBundleWriter.IsNonSourceSegment(Path.GetRelativePath(root, f)))
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        var clean = 0;
        var cutsByKind = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var failures = new List<string>();

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            var lexer = new LexerNs.Lexer(source);
            var tokens = lexer.TokenizeAll();
            if (lexer.Diagnostics.HasErrors)
                continue;
            clean++;
            var name = Path.GetRelativePath(FixtureRoots.CompilerTests.Path, file);
            if (lexer.LiteralStateUnknown)
                failures.Add($"{name}: lexes clean but the fact is set");

            foreach (var (kind, opener, closerStart) in MultiLineLiterals(source, tokens))
            {
                var openerLineEnd = source.IndexOf('\n', opener);
                var interiorLineEnd = openerLineEnd < 0 ? -1 : source.IndexOf('\n', openerLineEnd + 1);
                if (interiorLineEnd < 0 || interiorLineEnd >= closerStart)
                    continue;   // no complete interior line before the closer
                cutsByKind[kind] = cutsByKind.GetValueOrDefault(kind) + 1;
                var cut = Lex(source[..(interiorLineEnd + 1)]);
                if (!cut.LiteralStateUnknown)
                    failures.Add($"{name}: '{kind}' literal at offset {opener} cut after its first interior line, fact not set ({Errors(cut)})");
            }
        }

        _output.WriteLine($"files {files.Count}, lex clean {clean}, cuts {string.Join(", ", cutsByKind.Select(k => $"{(k.Key.Length == 0 ? "plain" : k.Key)}={k.Value}"))}");
        failures.Should().BeEmpty();
        clean.Should().BeGreaterThan(100, "the corpus is the compiler's fixture tree");
        cutsByKind.Keys.Should().Contain(new[] { "", "f" }, "the positive control must reach the plain and f-string read paths");
    }

    /// <summary>
    /// The abort-free loss mechanisms over the corpus (P22f, #2271), with the SAME documents the route-parity
    /// sweep's S6 drives through the LSP (<see cref="FormatterTwins.LiteralLossShapes"/>, one recipe), built from
    /// every clean fixture and its wide twin (the SPY0014 shape needs every level a multiple of 8). A document
    /// the lexer reads without aborting inside a literal, and in which some line that starts inside a literal in
    /// the fixture no longer does, sets the fact (<c>LiteralLoss</c> is not <c>None</c>) — by whichever mechanism
    /// lost it: a re-paired run whose closer line is dropped first is a <c>DroppedOpener</c> loss
    /// (<c>strings/triple_quoted_delimiter_lines</c>). Each shape has ≥ 1 such document that sets ITS mechanism's
    /// flag: every budget-stop position (<see cref="FormatterTwins.BudgetStopLines"/>) → <c>UnreadRemainder</c> (the
    /// compiler's lexer still stops; the editor's indent map does not, R-FP), repair → <c>RePairedCloser</c>, every
    /// dropped-delimiter code → <c>DroppedOpener</c>. Direction controls: a budget document of a fixture with NO
    /// literal spanning lines, and a <c>closeline-</c> document (a closer line that aborts and drops no quote, #2275)
    /// that loses no literal line, set nothing. The comment re-pair is not asserted: no lexer fact can see it
    /// (#2274). The <c>repair-SPY00NN</c> shapes (an orphan line that aborts after the re-paired closer, #2275) →
    /// <c>RePairedCloser</c> through the close-line rule (P22g Phase 3, R-FR). The own-quote axis (#2280, R-FV): every
    /// <c>repair-own-NAME</c> shape (the stray-closed twin, a literal line lost) → <c>RePairedCloser</c>; every
    /// <c>closeline-own-NAME</c> document that loses nothing is a direction control → <c>None</c> for the six tails the
    /// close-line walk pairs, <c>RePairedCloser</c> for the three it cannot (#2274's known limits, lead ruling L2).
    /// </summary>
    [Fact]
    public void Corpus_EveryAbortFreeLossShape_SetsItsMechanismsFlag()
    {
        var root = FixtureRoots.CompilerTests.Path;
        var files = Directory.EnumerateFiles(root, "*.spy", SearchOption.AllDirectories)
            .Where(f => !Sharpy.Compiler.Diagnostics.CrashBundleWriter.IsNonSourceSegment(Path.GetRelativePath(root, f)))
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        var expected = FormatterTwins.DroppedCodes.ToDictionary(c => FormatterTwins.DroppedShapePrefix + c, _ => LexerNs.LiteralLoss.DroppedOpener);
        foreach (var (budget, _) in FormatterTwins.BudgetStopLines)
            expected[budget] = LexerNs.LiteralLoss.UnreadRemainder;
        expected[FormatterTwins.RepairShape] = LexerNs.LiteralLoss.RePairedCloser;
        foreach (var code in FormatterTwins.CloseLineCodes)
            expected[FormatterTwins.RepairCloseLinePrefix + code.Code] = LexerNs.LiteralLoss.RePairedCloser;
        foreach (var tail in FormatterTwins.OwnQuoteTails)
            expected[FormatterTwins.RepairOwnPrefix + tail.Name] = LexerNs.LiteralLoss.RePairedCloser;
        var closeLineOwnControls = FormatterTwins.OwnQuoteTails.ToDictionary(t => FormatterTwins.CloseLineOwnPrefix + t.Name, t => (Tail: t, Count: 0));
        var limitTailsArmed = 0;
        var asserted = expected.Keys.ToDictionary(k => k, _ => 0);
        var ownFlag = expected.Keys.ToDictionary(k => k, _ => 0);
        var directionControls = 0;
        var closeLineControls = FormatterTwins.CloseLineCodes.ToDictionary(c => FormatterTwins.CloseLinePrefix + c.Code, _ => 0);
        var failures = new List<string>();

        foreach (var file in files)
        {
            var name = Path.GetRelativePath(root, file);
            var source = File.ReadAllText(file);
            foreach (var (q, wide) in new[] { (source, false), (FormatterTwins.WideIndented(source), true) })
            {
                var parent = new LexerNs.Lexer(q);
                var qLiteralLines = LexerNs.LiteralSpans.LinesStartingInside(q, LexerNs.LiteralSpans.Of(parent.TokenizeAll()));
                if (parent.Diagnostics.HasErrors)
                    continue;

                foreach (var shape in FormatterTwins.LiteralLossShapes(q, includeMismatch: wide))
                {
                    var lexer = new LexerNs.Lexer(shape.Text);
                    var dLiteralLines = LexerNs.LiteralSpans.LinesStartingInside(shape.Text, LexerNs.LiteralSpans.Of(lexer.TokenizeAll(), lexer.RecoveryResumes));
                    var where = $"{name}{(wide ? " (wide)" : "")} {shape.Shape}";
                    if (FormatterTwins.IsBudgetShape(shape.Shape) && shape.Kind == null)
                    {
                        directionControls++;
                        if (lexer.LiteralLoss != LexerNs.LiteralLoss.None)
                            failures.Add($"{where}: no literal spans lines, yet LiteralLoss is {lexer.LiteralLoss} ({Errors(lexer)})");
                        continue;
                    }

                    if (closeLineOwnControls.TryGetValue(shape.Shape, out var ownControl))
                    {
                        // R-FV: a closer line ending in a literal that aborts inside itself loses nothing — None, except
                        // the three tails the walk cannot pair (#2274's known limits, lead ruling L2), and those only where
                        // arm (b) is armed: a TRIPLE-quoted literal closed on that line (a single-quoted f-string whose hole
                        // spanned lines, fstrings/multiline_hole_2022, arms nothing — before R-FV as after).
                        if (!qLiteralLines.All(l => dLiteralLines.Contains(l + shape.LineShift)))
                            continue;
                        closeLineOwnControls[shape.Shape] = (ownControl.Tail, ownControl.Count + 1);
                        var armed = TailFollowsATripleCloser(shape.Text, ownControl.Tail);
                        if (ownControl.Tail.WalkCannotPair && armed)
                            limitTailsArmed++;
                        var want = ownControl.Tail.WalkCannotPair && armed ? LexerNs.LiteralLoss.RePairedCloser : LexerNs.LiteralLoss.None;
                        if (lexer.LiteralLoss != want)
                            failures.Add($"{where}: nothing is lost, so LiteralLoss should be {want}, yet it is {lexer.LiteralLoss} ({Errors(lexer)})");
                        continue;
                    }

                    if (closeLineControls.ContainsKey(shape.Shape))
                    {
                        // A closer line that aborts and drops no quote loses nothing; a document in which recovery
                        // went on to lose a literal line elsewhere is not this control's subject.
                        if (!qLiteralLines.All(l => dLiteralLines.Contains(l + shape.LineShift)))
                            continue;
                        closeLineControls[shape.Shape]++;
                        if (lexer.LiteralLoss != LexerNs.LiteralLoss.None)
                            failures.Add($"{where}: nothing is lost, yet LiteralLoss is {lexer.LiteralLoss} ({Errors(lexer)})");
                        continue;
                    }

                    if (!expected.TryGetValue(shape.Shape, out var flag)
                        || (lexer.LiteralLoss & LexerNs.LiteralLoss.AbortInsideLiteral) != 0
                        || qLiteralLines.All(l => dLiteralLines.Contains(l + shape.LineShift)))
                    {
                        continue;   // the comment re-pair, an abort (S5's subject), or nothing lost
                    }

                    asserted[shape.Shape]++;
                    if (lexer.LiteralLoss.HasFlag(flag))
                        ownFlag[shape.Shape]++;
                    else if (lexer.LiteralLoss == LexerNs.LiteralLoss.None)
                        failures.Add($"{where}: a literal line is lost without an abort, but LiteralLoss is None ({Errors(lexer)})");
                }
            }
        }

        _output.WriteLine($"lost without an abort / with the shape's own flag: {string.Join(", ", asserted.Select(a => $"{a.Key}={a.Value}/{ownFlag[a.Key]}"))}; "
            + $"direction controls (budget, no literal) {directionControls}; "
            + $"close-line direction controls (nothing lost) {string.Join(", ", closeLineControls.Select(c => $"{c.Key}={c.Value}"))}; "
            + $"own-quote close-line controls (nothing lost) {string.Join(", ", closeLineOwnControls.Select(c => $"{c.Key}={c.Value.Count}"))}, "
            + $"limit tails after a triple closer {limitTailsArmed}");
        failures.Should().BeEmpty();
        ownFlag.Where(a => a.Value == 0).Select(a => a.Key).Should().BeEmpty("every loss shape must reach the lexer with a lost literal line, no abort, and its own mechanism's flag");
        directionControls.Should().BeGreaterThan(100, "most fixtures hold no literal spanning lines");
        closeLineControls.Where(c => c.Value == 0).Select(c => c.Key).Should().BeEmpty("every close-line code must build a document that loses nothing");
        closeLineOwnControls.Where(c => c.Value.Count == 0).Select(c => c.Key).Should().BeEmpty("every own-quote tail must build a close-line document that loses nothing");
        limitTailsArmed.Should().BeGreaterThan(0, "the limit tails must reach arm (b) after a triple closer");
    }

    /// <summary>
    /// Whether the <c>closeline-own-</c> tail <paramref name="tail"/> in <paramref name="text"/> sits on the closer line
    /// of a TRIPLE-quoted literal (a triple delimiter earlier on its line: <c>""" + tail</c>, <c>}""")) + tail</c>) —
    /// the only close line arm (b) of <c>RePairedCloser</c> is armed on (<c>NoteMultiLineLiteralClosed</c>).
    /// </summary>
    private static bool TailFollowsATripleCloser(string text, FormatterTwins.OwnQuoteTail tail)
    {
        foreach (var quote in new[] { '"', '\'' })
        {
            var at = text.LastIndexOf(" + " + tail.TailFor(quote), StringComparison.Ordinal);
            if (at < 0)
                continue;
            var lineStart = text.LastIndexOfAny(new[] { '\n', '\r' }, at) + 1;
            var before = text[lineStart..at];
            return before.Contains("\"\"\"", StringComparison.Ordinal) || before.Contains("'''", StringComparison.Ordinal);
        }
        return false;
    }

    /// <summary>
    /// P22g: error recovery resumes on the next line without a <c>Newline</c> token, so the lexer records where
    /// (<see cref="LexerNs.Lexer.RecoveryResumes"/>). An f-string abandoned while its format spec is typed keeps its
    /// <c>FStringStart</c> in the stream; <see cref="LexerNs.LiteralSpans.Of"/> resets at the recovery point, so the
    /// next line's triple f-string — whose replacement field spans lines — keeps its span. The lexer lost nothing:
    /// the fact stays <c>None</c>. Positive control: the same tokens without the recovery points lose the span.
    /// </summary>
    [Fact]
    public void AnFStringAbandonedByRecovery_DoesNotSwallowTheNextLiteralsSpan()
    {
        const string source = "def main():\n    a = f\"{x:\n    print(f\"\"\"total: {\n        x * 2\n    }\"\"\")\n";
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();

        lexer.RecoveryResumes.Should().Equal(new[] { source.IndexOf("    print", StringComparison.Ordinal) }, "recovery resumes at the start of the line after the dropped one");
        lexer.LiteralLoss.Should().Be(LexerNs.LiteralLoss.None, Errors(lexer));
        LexerNs.LiteralSpans.LinesStartingInside(source, LexerNs.LiteralSpans.Of(tokens, lexer.RecoveryResumes))
            .Should().BeEquivalentTo(new[] { 4, 5 }, "the triple f-string's field and closer lines are literal");
        LexerNs.LiteralSpans.LinesStartingInside(source, LexerNs.LiteralSpans.Of(tokens))
            .Should().BeEmpty("positive control: without the recovery points the abandoned start pairs with the triple's end");
    }

    /// <summary>
    /// The recovery points whose dropped line continues on the next (<see cref="LexerNs.Lexer.RecoveryResumesAfterAContinuedLine"/>):
    /// a bracket left open AT THE LINE'S END (an abort inside a call or a list, an unterminated string inside one, a
    /// bracket opened in the text the lexer gave up) or a trailing backslash outside a comment. A dropped line that
    /// ends its statement — <c>""" + $</c> on a closer line, <c>x = $</c>, a header whose bracket the given-up text
    /// closes (<c>if foo($):</c>: the next line is its BODY), a backslash inside a comment, a bracket inside a string
    /// or a comment — is not one. (P22g verify round: the first spelling read the depth at the abort, so
    /// <c>if foo($):</c> continued and <c>x = $ foo(1,</c> did not.)
    /// </summary>
    [Theory]
    [InlineData("def main():\n    x = foo($,\n        a)\n", true)]
    [InlineData("def main():\n    x = foo(\"abc,\n        a)\n", true)]
    [InlineData("def main():\n    xs = [1, 2, $\n        3]\n", true)]
    [InlineData("def main():\n    y = 1 + $ \\\n        2\n", true)]
    [InlineData("def main():\n    x = $ foo(1,\n        2)\n", true)]
    [InlineData("def main():\n    x = foo(\"\\q\", [1,\n        2])\n", true)]
    [InlineData("def main():\n    d = {\n        'a': $, 'b': {\n        }}\n", true)]
    [InlineData("def main():\n    s = \"\"\"\n    a\n    \"\"\" + $\n    print(s)\n", false)]
    [InlineData("def main():\n    x = $\n    y = 1\n", false)]
    [InlineData("def main():\n    if foo($):\n        y = 1\n", false)]
    [InlineData("def main():\n    if x in [1, $]:\n        y = 1\n", false)]
    [InlineData("class Foo:\n    def method(self, $) -> None:\n        ...\n", false)]
    [InlineData("def main():\n    x = foo(\"\\q\", 1)\n    y = 1\n", false)]
    [InlineData("def main():\n    x = $  # path C:\\\n    y = 1\n", false)]
    [InlineData("def main():\n    x = $ \"(\"\n    y = 1\n", false)]
    [InlineData("def main():\n    x = foo(1, $)  # (\n    y = 1\n", false)]
    [InlineData("def main():\n    x = $ `(`\n    y = 1\n", false)]
    [InlineData("def main():\n    x = (1, f\"{x:\" + 2)\n    y = 1\n", false)]
    public void ARecoveryPoint_KnowsWhetherTheDroppedLineContinues(string source, bool continues)
    {
        var lexer = Lex(source);

        lexer.RecoveryResumes.Should().ContainSingle(Errors(lexer));
        lexer.RecoveryResumesAfterAContinuedLine.Contains(lexer.RecoveryResumes[0]).Should().Be(continues, source);
    }

    /// <summary>
    /// The seam-1 matrix of #2279 (plan-f92797 P22h, R-FU): <see cref="LexerNs.Lexer.BracketLeftOpenAtLine"/> is the
    /// 1-based line of the OPENER of the outermost bracket the lexer never saw closed, recorded at the FIRST of the
    /// end of the source with a bracket open and a recovery whose dropped line has a bracket open at its END — a real
    /// bracket (on the statement's first line or a continuation line), one "closed" inside an unterminated string or
    /// backtick name (open to the lexer), one opened before the abort or in the given-up text, one opened on an
    /// EARLIER line and abandoned on a dropped continuation line (the opener's line, not the dropped one). Positive
    /// controls that record NOTHING: a bracket the given-up text closes (A8, the five
    /// <c>BracketClosedOnTheDroppedLine</c> rows of the LSP tests), a backslash-continued dropped line (A9 — it
    /// continues, but leaves no bracket open), a bracket in a string, a comment or a closed backtick name on the
    /// dropped line (<see cref="ARecoveryPoint_KnowsWhetherTheDroppedLineContinues"/>'s rows), a bracket inside an
    /// unclosed f-string field (<c>ReportUnclosedField</c>'s shape, not this fact), and clean documents.
    /// </summary>
    public static TheoryData<string, string, int?> BracketLeftOpen => new()
    {
        // The end of the source with a bracket open.
        { "eof: opened on the statement's first line (A1)", "def main():\n    xs = [1,\n    y = 2\n    if y:\n        print(y)\n", 2 },
        { "eof: the statement's continuation line is the last", "def main():\n    x = foo(\n        1,\n", 2 },
        { "eof: opened on a backslash continuation line", "def main():\n    x = 1 + \\\n        foo(\n            1,\n", 3 },
        { "eof: nested, the outermost opener", "def main():\n    x = foo(1,\n        [2,\n", 2 },
        { "eof: above a class (A11)", "x = (\nclass Foo:\n    def m(self):\n        pass\n", 1 },
        { "eof: a decorator (A2)", "@[foo\nclass Bar:\n    pass\n", 1 },
        { "eof: after a recovery that leaves none open", "def main():\n    x = $\n    y = [1,\n", 3 },
        { "eof: CRLF", "def main():\r\n    xs = [1,\r\n    y = 2\r\n", 2 },
        { "eof: abort on the last line, opened before it", "def main():\n    x = foo(1,\n        \"abc", 2 },
        { "eof: abort on the last line, opened in the given-up text", "def main():\n    x = $ foo(1,", 2 },
        // A recovery whose dropped line has a bracket open at its END.
        { "closed inside an unterminated string (A3)", "def main():\n    if foo(\"abc):\n        y = 1\n    z = 2\n  w = 3\n", 2 },
        { "closed inside an unterminated backtick name (A4)", "def main():\n    if foo(`x):\n        y = 1\n    z = 2\n  w = 3\n", 2 },
        { "opened before the abort (A5)", "def main():\n    x = foo($,\ndef g():\n  return 1\n", 2 },
        { "opened in the given-up text (A7)", "def main():\n    x = $ foo(1,\n          2)\n  y = 1\n", 2 },
        { "opened in the given-up text below a clean line", "def main():\n    q = 1\n    x = $ foo(1,\n          2)\n", 3 },
        { "opened on an earlier line, abandoned on a dropped continuation line", "def main():\n    x = foo(1,\n        $, 2\n    y = 1\n", 2 },
        { "closed and re-opened in the given-up text of a continuation line", "def main():\n    x = foo(1,\n        $) + bar(2,\n    y = 1\n", 3 },
        { "around an unclosed f-string field", "def main():\n    print(f\"{x\n    y = 1\n", 2 },
        { "CR line breaks", "def main():\r    x = foo($,\r    y = 1\r", 2 },
        // Two events: the first.
        { "two recoveries: the first", "def main():\n    x = foo($,\n    y = bar($,\n    z = 1\n", 2 },
        { "a recovery, then the end of the source: the first", "def main():\n    x = foo($,\n    y = [1,\n", 2 },
        // Positive controls: nothing is left open.
        { "closed in the given-up text (A8)", "def main():\n    if foo($):\n          y = 1\n", null },
        { "BracketClosedOnTheDroppedLine: if call", "def main():\n    if foo($):\n        y = 1\n    z = 2\n  w = 3\n", null },
        { "BracketClosedOnTheDroppedLine: for range", "def main():\n    for i in range($):\n        y = 1\n    z = 2\n  w = 3\n", null },
        { "BracketClosedOnTheDroppedLine: with open", "def main():\n    with open($) as h:\n        y = 1\n    z = 2\n  w = 3\n", null },
        { "BracketClosedOnTheDroppedLine: list in if", "def main():\n    if x in [1, $]:\n        y = 1\n    z = 2\n  w = 3\n", null },
        { "BracketClosedOnTheDroppedLine: signature", "class Foo:\n    def method(self, $) -> None:\n        ...\n  x = 1\n", null },
        { "closed on its dropped continuation line", "def main():\n        x = foo(\n            1, $)\n        print(x)\n", null },
        { "backslash-continued (A9)", "def main():\n    y = 1 + $ \\\n          2\n  z = 1\n", null },
        { "a backslash in a comment", "def main():\n    x = $  # path C:\\\n    y = 1\n", null },
        { "a bracket in a string", "def main():\n    x = $ \"(\"\n    y = 1\n", null },
        { "a bracket in a comment", "def main():\n    x = foo(1, $)  # (\n    y = 1\n", null },
        { "a bracket in a closed backtick name", "def main():\n    x = $ `(`\n    y = 1\n", null },
        { "closed after an f-string's spec abort", "def main():\n    x = (1, f\"{x:\" + 2)\n    y = 1\n", null },
        { "a bracket inside an unclosed f-string field", "def main():\n    x = f\"{foo(1,\n    y = 1\n", null },
        { "an unclosed f-string field's own brace", "def main():\n    x = f\"{x\n    y = 1\n", null },
        { "a recovery with no bracket", "def main():\n    x = $\n    y = 1\n", null },
        { "eof: abort on the last line, closed", "def main():\n    x = foo($)", null },
        { "clean: a bracket closed on a later line", "def main():\n    xs = [1,\n        2]\n    print(xs)\n", null },
        { "clean: no bracket spans a line", "def main():\n    print(1)\n", null },
    };

    [Theory]
    [MemberData(nameof(BracketLeftOpen))]
    public void ARecoveryPointOrTheEndOfTheSource_RecordsTheOpenerLineOfABracketLeftOpen(string name, string source, int? openerLine)
    {
        var lexer = Lex(source);

        lexer.BracketLeftOpenAtLine.Should().Be(openerLine, $"{name} ({Errors(lexer)})");
    }

    /// <summary>
    /// The continuation shapes over the corpus (plan-f92797 Design 6), with the SAME documents the route-parity sweep's
    /// S7 drives through the LSP (<see cref="FormatterTwins.ContinuationShapes"/>, one recipe), built from every clean
    /// fixture, its wide twin and its lone-CR twin. A bracket shape records
    /// <see cref="LexerNs.Lexer.BracketLeftOpenAtLine"/> at the injected line — the first event of the document (Q is
    /// clean above it), whether the lexer follows the bracket to the end of the source (<c>bracket-eof</c>) or a
    /// recovery abandons it (<c>bracket-string</c>, <c>bracket-backtick</c>, <c>bracket-dropped</c>). A
    /// <c>recovery*</c> shape resumes at the line after the injected one, does NOT continue (no bracket, no
    /// backslash), and records no bracket at the injected line (a LATER event may — an indentation error that drops a
    /// line opening a bracket, or a bracket of Q's own left open to the end of the source — counted). A document that keeps every literal line of Q loses no literal
    /// (<c>LiteralLoss.None</c>); only <c>recovery-misindented</c> may lose one — its re-indented line, at a width on no
    /// level, is dropped (SPY0013), and when it opens a literal that spans lines the fact must be set (S6's subject,
    /// counted). Each shape is counted and must build ≥ 1 document.
    /// </summary>
    [Fact]
    public void Corpus_EveryContinuationShape_RecordsItsFactAndLosesNoLiteral()
    {
        var root = FixtureRoots.CompilerTests.Path;
        var files = Directory.EnumerateFiles(root, "*.spy", SearchOption.AllDirectories)
            .Where(f => !Sharpy.Compiler.Diagnostics.CrashBundleWriter.IsNonSourceSegment(Path.GetRelativePath(root, f)))
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        var bracketShapes = new[] { FormatterTwins.BracketEofShape, FormatterTwins.BracketStringShape, FormatterTwins.BracketBacktickShape, FormatterTwins.BracketDroppedShape };
        var documents = FormatterTwins.ContinuationShapeNames.ToDictionary(s => s, _ => 0);
        var laterFacts = FormatterTwins.ContinuationShapeNames.ToDictionary(s => s, _ => 0);
        var lostLiteralLine = FormatterTwins.ContinuationShapeNames.ToDictionary(s => s, _ => 0);
        var samples = new List<string>();
        var failures = new List<string>();

        foreach (var file in files)
        {
            var name = Path.GetRelativePath(root, file);
            var source = File.ReadAllText(file);
            foreach (var (q, twin) in new[] { (source, ""), (FormatterTwins.WideIndented(source), " (wide)"), (FormatterTwins.Cr(source), " (cr)") })
            {
                var parent = new LexerNs.Lexer(q);
                var qLiteralLines = LexerNs.LiteralSpans.LinesStartingInside(q, LexerNs.LiteralSpans.Of(parent.TokenizeAll()));
                if (parent.Diagnostics.HasErrors)
                    continue;

                foreach (var shape in FormatterTwins.ContinuationShapes(q))
                {
                    var lexer = new LexerNs.Lexer(shape.Text);
                    var dLiteralLines = LexerNs.LiteralSpans.LinesStartingInside(shape.Text, LexerNs.LiteralSpans.Of(lexer.TokenizeAll(), lexer.RecoveryResumes));
                    var where = $"{name}{twin} {shape.Shape}";
                    documents[shape.Shape]++;
                    if (qLiteralLines.All(l => dLiteralLines.Contains(l < shape.InsertedAt ? l : l + 1)))
                    {
                        if (lexer.LiteralLoss != LexerNs.LiteralLoss.None)
                            failures.Add($"{where}: no literal line is lost, yet LiteralLoss is {lexer.LiteralLoss} ({Errors(lexer)})");
                    }
                    else if (shape.Shape != FormatterTwins.RecoveryMisindentedShape && !FormatterTwins.IsRecoveryHeaderShape(shape.Shape))
                    {
                        failures.Add($"{where}: the shape loses a literal line ({Errors(lexer)})");
                    }
                    else
                    {
                        // The line re-indented to a width on no level is dropped (SPY0013, or SPY0011/SPY0012 for the
                        // recovery-header-tab shape); when it opens a literal that spans lines, that literal is lost — S6's
                        // subject, so the fact must say so (recovery-misindented and the recovery-header-* shapes, L8).
                        lostLiteralLine[shape.Shape]++;
                        if (lexer.LiteralLoss == LexerNs.LiteralLoss.None)
                            failures.Add($"{where}: a literal line is lost, yet LiteralLoss is None ({Errors(lexer)})");
                    }

                    var injected = shape.InsertedAt + 1;
                    if (bracketShapes.Contains(shape.Shape))
                    {
                        if (lexer.BracketLeftOpenAtLine != injected)
                            failures.Add($"{where}: the bracket opens on line {injected}, yet BracketLeftOpenAtLine is {lexer.BracketLeftOpenAtLine?.ToString() ?? "null"} ({Errors(lexer)})");
                        continue;
                    }

                    var resume = LineStart(shape.Text, shape.InsertedAt + 1);
                    if (!lexer.RecoveryResumes.Contains(resume))
                        failures.Add($"{where}: no recovery resumes at the line after the injected one (offset {resume}; resumes {string.Join(", ", lexer.RecoveryResumes)})");
                    else if (lexer.RecoveryResumesAfterAContinuedLine.Contains(resume))
                        failures.Add($"{where}: the injected line ends its statement, yet its recovery point is a continued one");
                    if (lexer.BracketLeftOpenAtLine is { } line)
                    {
                        if (line <= injected)
                            failures.Add($"{where}: no bracket opens at or above line {injected}, yet BracketLeftOpenAtLine is {line} ({Errors(lexer)})");
                        else
                        {
                            laterFacts[shape.Shape]++;
                            if (laterFacts[shape.Shape] <= 2)
                                samples.Add($"{where}: line {line} ({Errors(lexer)})");
                        }
                    }
                }
            }
        }

        _output.WriteLine($"documents per shape: {string.Join(", ", documents.Select(d => $"{d.Key}={d.Value}"))}; "
            + $"a literal line lost by the dropped misindented line: {string.Join(", ", lostLiteralLine.Where(d => d.Value > 0).Select(d => $"{d.Key}={d.Value}"))}; "
            + $"a bracket left open by a LATER event: {string.Join(", ", laterFacts.Select(d => $"{d.Key}={d.Value}"))}; e.g. {string.Join(" || ", samples)}");
        failures.Should().BeEmpty();
        documents.Where(d => d.Value == 0).Select(d => d.Key).Should().BeEmpty("every continuation shape must build ≥ 1 document from the corpus");
    }

    /// <summary>The offset where 0-based <paramref name="line"/> of <paramref name="text"/> starts (<c>\r\n</c>, <c>\r</c> and <c>\n</c> each end a line).</summary>
    private static int LineStart(string text, int line)
    {
        var i = 0;
        for (var l = 0; l < line && i < text.Length; l++)
        {
            while (i < text.Length && text[i] is not ('\n' or '\r'))
                i++;
            if (i < text.Length && text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                i++;
            i++;
        }
        return i;
    }

    /// <summary>
    /// Lead ruling L8 (verifier 1a, 1b; #2279): <see cref="LexerNs.Lexer.RecoveryResumesAfterAPossibleHeader"/> holds the
    /// resumes whose dropped line, read as the lexer reads it (the logical line's tokens before the abort plus the
    /// given-up text, strings, comments and backtick names opaque), holds a <c>:</c> at bracket depth 0 outside any
    /// f-string — a line that could open a block, so the editor must not re-indent the line below it. Members: a header
    /// whose colon is given up (<c>if foo($):</c>, <c>while $:</c>, a signature, a header spanning lines) or read before
    /// the abort (<c>if x: $</c>), and conservatively an annotation and a lambda. Non-members: a colon in a bracket, a
    /// string (closed or swallowing it), a comment, a field, a backtick name, a walrus; a continued line (bracket or
    /// backslash) whose colon was read; a line dropped at an indentation error (no token read); a colon on an EARLIER
    /// logical line (the flag ends at its Newline, and at a recovery). Line breaks LF, CRLF and lone CR; a tab-indented
    /// recovery line (1b). <paramref name="resume"/> picks the recovery point (0 = the first).
    /// </summary>
    public static TheoryData<string, string, int, bool> PossibleHeader => new()
    {
        { "if foo($): (1a)", "def main():\n    if foo($):\n      y = 1\n", 0, true },
        { "if foo($): body at its width", "def main():\n    if foo($):\n        y = 1\n", 0, true },
        { "if x: $ (the colon read before the abort)", "def main():\n    if x: $\n        y = 1\n", 0, true },
        { "while $:", "def main():\n    while $:\n        y = 1\n", 0, true },
        { "elif foo($):", "def main():\n    if a:\n        b = 1\n    elif foo($):\n        y = 1\n", 0, true },
        { "def m(self, $):", "class C:\n    def m(self, $):\n        return 1\n", 0, true },
        { "for i in range($):", "def main():\n    for i in range($):\n        a = i\n        b = a\n    print(b)\n", 0, true },
        { "module-level if foo($):", "if foo($):\n  y = 1\n", 0, true },
        { "a header spanning lines", "def main():\n    if foo(1,\n            $):\n        y = 1\n", 0, true },
        { "x: int = $ (an annotation, conservative)", "def main():\n    x: int = $\n    y = 1\n", 0, true },
        { "lambda x: $", "def main():\n    f = lambda x: $\n    y = 1\n", 0, true },
        { "CRLF", "def main():\r\n    if foo($):\r\n      y = 1\r\n", 0, true },
        { "lone CR", "def main():\r    if foo($):\r      y = 1\r", 0, true },
        { "a tab-indented recovery line (1b)", "def main():\n    if foo($):\n\t\ty = 1\n    z = 2\n", 0, true },
        { "x = $ (A6)", "def main():\n    x = $\n      y = 1\n", 0, false },
        { "a colon inside a closed brace", "def main():\n    d = {a: $}\n    y = 1\n", 0, false },
        { "a colon inside a slice", "def main():\n    b = a[1:$]\n    y = 1\n", 0, false },
        { "a colon in a closed string", "def main():\n    s = \"a:\" + $\n    y = 1\n", 0, false },
        { "a colon swallowed by an unterminated string", "def main():\n    s = \"a:$\n    y = 1\n", 0, false },
        { "a header colon swallowed by an unterminated string (L6's domain)", "def main():\n    if x == \"abc:\n        y = 1\n", 0, false },
        { "a colon in a comment", "def main():\n    x = $  # a: b\n    y = 1\n", 0, false },
        { "a colon in a format spec", "def main():\n    s = f\"{x:>3}\" + $\n    y = 1\n", 0, false },
        { "a colon in a field's slice", "def main():\n    s = f\"{a[1:2]}\" + $\n    y = 1\n", 0, false },
        { "a colon in a backtick name", "def main():\n    x = `a:b` + $\n    y = 1\n", 0, false },
        { "a walrus", "def main():\n    y := $\n    z = 1\n", 0, false },
        { "a colon read on a line a bracket continues", "def main():\n    if x: foo($,\n        2)\n", 0, false },
        { "a colon read on a line a backslash continues", "def main():\n    if x: y = 1 + $ \\\n        2\n", 0, false },
        { "an indentation drop (no token read)", "def main():\n    x = 1\n      if y:\n    z = 2\n", 0, false },
        { "a colon on the logical line BEFORE (its Newline ends it)", "def main():\n    if x:\n        y = $\n        z = 1\n", 0, false },
        { "a colon on the line a recovery dropped BEFORE", "def main():\n    if x: $\n    y = $\n    z = 1\n", 1, false },
    };

    [Theory]
    [MemberData(nameof(PossibleHeader))]
    public void ARecoveryPoint_KnowsWhetherTheDroppedLineCouldOpenABlock(string name, string source, int resume, bool possibleHeader)
    {
        var lexer = Lex(source);

        lexer.RecoveryResumes.Count.Should().BeGreaterThan(resume, $"{name} ({Errors(lexer)})");
        lexer.RecoveryResumesAfterAPossibleHeader.Contains(lexer.RecoveryResumes[resume]).Should().Be(possibleHeader, $"{name} ({Errors(lexer)})");
        lexer.RecoveryResumesAfterAPossibleHeader.Should().NotIntersectWith(lexer.RecoveryResumesAfterAContinuedLine, "a continued line's next line is its continuation, never a body");
    }

    /// <summary>
    /// Lead ruling L10 (verifier 1d, #2279): the continued-line judgment and the bracket fact read ONE depth, the fields'
    /// brackets taken off. <c>x = f"{foo($)}"</c> (a bracket inside a field) is neither continued nor a bracket left open
    /// — before L10 it was a continued resume with no fact, so the line below was neither frozen nor judged and moved
    /// into the <c>if</c> above. A REAL bracket around the f-string is both (<c>print(f"{foo($)}"</c>). The unclosed-field
    /// path agrees (<c>x = f"{foo(1,</c> neither; <c>print(f"{foo(1,</c> both).
    /// </summary>
    [Theory]
    [InlineData("1d: a paren inside a field", "def main():\n    if c:\n        x = f\"{foo($)}\"\n    print(x)\n", false, null)]
    [InlineData("1d: a bracket inside a field", "def main():\n    if c:\n        x = f\"{[$]}\"\n    print(x)\n", false, null)]
    [InlineData("a real bracket around the f-string", "def main():\n    print(f\"{foo($)}\"\n    y = 1\n", true, 2)]
    [InlineData("a field's bracket and a real one, both closed", "def main():\n    print(f\"{foo($)}\")\n    y = 1\n", false, null)]
    [InlineData("an unclosed field with a bracket inside it", "def main():\n    x = f\"{foo(1,\n    y = 1\n", false, null)]
    [InlineData("an unclosed field inside a real bracket", "def main():\n    print(f\"{foo(1,\n    y = 1\n", true, 2)]
    public void TheContinuedJudgmentAndTheBracketFact_ReadOneDepth(string name, string source, bool continues, int? openerLine)
    {
        var lexer = Lex(source);

        lexer.RecoveryResumes.Should().NotBeEmpty($"{name} ({Errors(lexer)})");
        lexer.RecoveryResumesAfterAContinuedLine.Contains(lexer.RecoveryResumes[0]).Should().Be(continues, $"{name} ({Errors(lexer)})");
        lexer.BracketLeftOpenAtLine.Should().Be(openerLine, $"{name} ({Errors(lexer)})");
    }

    /// <summary>A lex that recovers from nothing resumes nowhere.</summary>
    [Fact]
    public void ACleanLex_RecordsNoRecoveryPoint()
    {
        var lexer = new LexerNs.Lexer("def main():\n    a = f\"{x}\"\n    print(a)\n");
        lexer.TokenizeAll();
        lexer.RecoveryResumes.Should().BeEmpty();
    }

    /// <summary>(prefix, opener offset, offset of the closing delimiter) of every triple-quoted literal.</summary>
    private static IEnumerable<(string Kind, int Opener, int CloserStart)> MultiLineLiterals(string source, List<LexerNs.Token> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Type is TokenType.String or TokenType.RawString or TokenType.ByteString && token.SourceLength is { } length)
            {
                var quote = source.IndexOfAny(new[] { '"', '\'' }, token.Position);
                if (quote + 2 < source.Length && source[quote + 1] == source[quote] && source[quote + 2] == source[quote]
                    && length >= quote - token.Position + 6)
                    yield return (source[token.Position..quote], token.Position, token.Position + length - 3);
            }
            else if (token.Type == TokenType.FStringStart
                && (token.Value.EndsWith("\"\"\"", StringComparison.Ordinal) || token.Value.EndsWith("'''", StringComparison.Ordinal)))
            {
                var depth = 0;
                for (var j = i; j < tokens.Count; j++)
                {
                    if (tokens[j].Type == TokenType.FStringStart)
                        depth++;
                    else if (tokens[j].Type == TokenType.FStringEnd && --depth == 0)
                    {
                        yield return (token.Value[..^3], token.Position, tokens[j].Position);
                        break;
                    }
                }
            }
        }
    }
}
