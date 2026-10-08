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
    /// (#2274). The <c>repair-SPY00NN</c> shapes join at P22g Phase 3 (their rows live in the route-parity sweep's
    /// allowlist until then).
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
            + $"close-line direction controls (nothing lost) {string.Join(", ", closeLineControls.Select(c => $"{c.Key}={c.Value}"))}");
        failures.Should().BeEmpty();
        ownFlag.Where(a => a.Value == 0).Select(a => a.Key).Should().BeEmpty("every loss shape must reach the lexer with a lost literal line, no abort, and its own mechanism's flag");
        directionControls.Should().BeGreaterThan(100, "most fixtures hold no literal spanning lines");
        closeLineControls.Where(c => c.Value == 0).Select(c => c.Key).Should().BeEmpty("every close-line code must build a document that loses nothing");
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
