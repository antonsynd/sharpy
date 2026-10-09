using FluentAssertions;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// <see cref="LexerNs.Lexer.HoldsALiteralSpanningLines"/> (P22f decision 2, #2271): the one scanner that
/// decides whether a dropped span or the source left at an error-budget stop holds a literal that can
/// span lines. Every row is a literal, not generated: each pins one arm of the walk.
/// </summary>
public class LiteralLossScannerTests
{
    [Theory]
    // a triple opener whose closer is absent or on a later line
    [InlineData("s = \"\"\"", true)]
    [InlineData("\"\"\"", true)]
    [InlineData("s = '''", true)]
    [InlineData("s = \"\"\"a\nb\"\"\"", true)]
    [InlineData("def main():\n    s = \"\"\"\n    k\n    \"\"\"\n", true)]
    [InlineData("s = \"\"\"\r", true)]
    [InlineData("s = \"\"\"\r\n", true)]
    [InlineData("s = \"\"\"abc\"\"\" + \"\"\"", true)]       // a closed triple, then an opener on the same line
    [InlineData("x = $\"\"\"", true)]                        // the X6 span (SPY0015 drops the rest of the line)
    [InlineData("x = `it's` + \"\"\"", true)]                     // a backtick-delimited name is opaque (`it's` opens no string)
    [InlineData("x = `a#b` + \"\"\"", true)]
    [InlineData("x = `a\"\"\"b`", false)]
    [InlineData("x = \"#\" + \"\"\"", true)]                 // a '#' inside a short string is not a comment
    [InlineData("x = r\"\\\" + \"\"\"", true)]               // r"\" closes at its second quote: no escape in a raw literal
    // a triple closed on its own line, or held by a short string, opens nothing
    [InlineData("s = \"\"\"abc\"\"\"", false)]
    [InlineData("x = \"hi\"", false)]
    [InlineData("t = '\"\"\"'", false)]
    [InlineData("t = '\"\"\"' + x", false)]
    [InlineData("x = \"a\\\"\"\"", false)]                   // "a\"" then an unterminated short string
    [InlineData("def main():\n    x = 1\n", false)]
    // comments
    [InlineData("# s = \"\"\"", false)]
    [InlineData("x = 1  # \"\"\"", false)]
    // an f-/t-string whose replacement field is left open at the line break
    [InlineData("x = f\"{', '.join(", true)]
    [InlineData("x = f\"{a", true)]
    [InlineData("x = t\"{a", true)]
    [InlineData("x = df\"{a", true)]
    [InlineData("x = f\"{a}\" + \"b", false)]               // a closed hole, then an unterminated short string
    [InlineData("x = f\"{x", true)]
    [InlineData("x = f\"{{{x", true)]                        // an escaped brace, then an open field
    // an unterminated f-/t-string with no OPEN field ends at its line like any short string
    [InlineData("x = f\"hello", false)]
    [InlineData("x = t\"hello", false)]
    [InlineData("x = df\"hello", false)]
    [InlineData("x = f\"{x}", false)]                        // the field is closed
    [InlineData("x = f\"total: {x}", false)]
    [InlineData("x = f\"{{", false)]                         // an escaped brace opens no field
    // any other unterminated short string ends at its line
    [InlineData("x = r\"abc", false)]
    [InlineData("x = d\"abc", false)]
    [InlineData("x = b\"abc", false)]
    [InlineData("x = xf\"{a", false)]                        // xf is an identifier: a plain string follows
    // the known limit, pinned so the choice is recorded (#2274): the opener is swallowed by a short string
    [InlineData("x = 'abc \"\"\"", false)]
    public void HoldsALiteralSpanningLines(string text, bool expected)
    {
        LexerNs.Lexer.HoldsALiteralSpanningLines(text).Should().Be(expected, text);
    }

    /// <summary>
    /// Inside a replacement field a quote opens a NESTED literal, read by the same rules as the lexer's hole
    /// reading (<c>NextFStringToken</c>): its quotes and braces are not the enclosing literal's. The second
    /// verification of P22f @ 37e5c1d02 measured E1par/E1par2 and L4close4 (content rewritten: a nested quote
    /// closed the f-string, a nested <c>}</c> closed the field) and W12/M17/M16 (repair lost: a nested
    /// <c>{</c> opened a field, a closed nested triple read as an opener).
    /// </summary>
    [Theory]
    // the enclosing literal closes after its nested ones, then an opener: true
    [InlineData("x = f\"{'\"'}\\q\" + \"\"\"", true)]            // E1par2's span (it starts at f")
    [InlineData("x = f\"{\"\\\"\"}\" + \"\"\"", true)]            // E1par: a nested string in the enclosing quote
    [InlineData("x = f\"{\"\\q\"}\" + \"\"\"", true)]             // E1hole
    [InlineData("x = f\"{'#'}\" + \"\"\"", true)]                 // a '#' in a nested string is not a comment
    [InlineData("x = f\"{f'{x}'}\" + \"\"\"", true)]              // a nested f-string with its own field
    // a field still open at the line break: true
    [InlineData("y = f\"{'}'", true)]                             // a '}' in a nested string closes nothing
    [InlineData("y = f\"{d['}'] +", true)]                        // L4close4's span
    [InlineData("y = t\"{d[\"}\"] +", true)]
    [InlineData("y = f\"{f'{x'", true)]                           // the nested f-string ends at its own quote
    [InlineData("y = f\"{'abc", true)]                            // the nested short string ends at its line
    [InlineData("y = f\"{x # }\" + 'z'", true)]                   // a comment in a field runs to the end of the line
    [InlineData("y = f\"{x:{'}'}", true)]                         // a nested field in a format spec
    // a nested triple left open, or a triple f-string whose field holds its quotes: true
    [InlineData("y = f\"{\"\"\"a", true)]
    [InlineData("y = f\"\"\"{'\"\"\"'}", true)]
    // every literal closed on the line: false
    [InlineData("y = f\"{'{'}", false)]                           // W12: a '{' in a nested string opens nothing
    [InlineData("y = \"\\q\" + f\"{'{'}\\q", false)]              // M17's span
    [InlineData("y = df\"{'{'}\"", false)]
    [InlineData("y = f\"{\"\"\"a\"\"\"}\\q\" + 'z'", false)]      // M16: a closed nested triple
    [InlineData("y = f\"{f'{x}'}\"", false)]
    [InlineData("y = f\"{x!r:>{w}}\"", false)]                   // a nested field in a format spec
    [InlineData("y = f\"{x:'}\" + 'z'", false)]                   // a quote in a format spec is spec text
    [InlineData("y = f\"{`a}'`}\" + 'z'", false)]                 // a backtick-delimited name is opaque
    [InlineData("y = f\"{x\" + 'z'", false)]                      // the lexer's `expecting '}'`: the enclosing quote closes it
    [InlineData("y = f\"\"\"{'\"\"\"'}\"\"\"", false)]
    [InlineData("y = f\"{'a' 'b'}\" + 'c'", false)]
    public void HoldsALiteralSpanningLines_ReadsALiteralNestedInAReplacementField(string text, bool expected)
    {
        LexerNs.Lexer.HoldsALiteralSpanningLines(text).Should().Be(expected, text);
    }

    /// <summary>
    /// #2276: at the literal's own quote inside a format spec the lexer aborts (SPY0022, <c>expecting '}'</c>) and reads
    /// nothing more on the line; the scanner walks the rest afresh with the quote in either role and holds a literal
    /// spanning lines when either walk does. SQ2/SQ3's span (<c>f"{x:">}" + """</c>) was false @ 926670989 — the
    /// scanner closed the f-string at the quote and paired <c>" + "</c>, then <c>""</c>, swallowing the triple. The
    /// typing control (<c>f"{x:"</c>), the closed spec, a trailing comment or short string hold none; no spec text
    /// before the quote (<c>f"{x:" + """</c>, the walk past the quote) holds one; t-, df- and triple twins.
    /// </summary>
    [Theory]
    [InlineData("x = f\"{x:\">}\" + \"\"\"", true)]               // SQ2 / SQ3: the walk from the quote
    [InlineData("x = f\"{x:\" + \"\"\"", true)]                    // the walk from past the quote
    [InlineData("x = t\"{x:\">}\" + \"\"\"", true)]
    [InlineData("x = df\"{x:\">}\" + \"\"\"", true)]
    [InlineData("x = f\"\"\"{x:\"\"\"", true)]                      // the triple twin (also an abort inside the literal)
    [InlineData("x = f\"{x:\"", false)]                              // being typed: no on-type refusal
    [InlineData("x = f\"{x:\">}\"", false)]
    [InlineData("x = f\"{x:\">}\"  # \"\"\"", false)]
    [InlineData("x = f\"{x:\">}\" + 'z'", false)]
    [InlineData("y = f\"{x:'}\" + 'z'", false)]                  // the OTHER quote is spec text (unchanged)
    public void HoldsALiteralSpanningLines_AtTheLexersSpecQuoteAbort(string text, bool expected)
    {
        LexerNs.Lexer.HoldsALiteralSpanningLines(text).Should().Be(expected, text);
    }

    /// <summary>
    /// #2276's sibling axis — the other aborts inside an f-string the scanner reads as structure — already agree with
    /// the lexer (controls): SPY0020 (the enclosing quote ends an open field: the scanner's ClosesOnItsLine rule),
    /// SPY0021 (an unmatched <c>}</c>), SPY0030 (an invalid conversion); each with its clean-close twin.
    /// </summary>
    [Theory]
    [InlineData("x = f\"{x\" + '''", true)]                        // SPY0020
    [InlineData("x = f\"{x\" + 'z'", false)]
    [InlineData("x = f\"}\" + \"\"\"", true)]                         // SPY0021
    [InlineData("x = f\"{x}}\" + \"\"\"", true)]
    [InlineData("x = f\"}\" + 'z'", false)]
    [InlineData("x = f\"{x}}\"", false)]                           // the SPY0021 twin with a clean close after it
    [InlineData("x = f\"{x\" + \"\"\"", true)]                      // not SPY0020: the lexer's prescan reads `" + "` as a nested string, then `""`, and reports SPY0022 at the bracket — the hole is left open on its line, which the scanner reads as a loss (the lexer's dropped span starts AT the bracket, outside the literal, and holds no opener: LiteralStateTests)
    [InlineData("x = f\"{x!q}\" + \"\"\"", true)]                      // SPY0030
    [InlineData("x = f\"{x!q}\" + 'z'", false)]
    public void HoldsALiteralSpanningLines_TheOtherMidLiteralAborts_AgreeWithTheLexer(string text, bool expected)
    {
        LexerNs.Lexer.HoldsALiteralSpanningLines(text).Should().Be(expected, text);
    }

    /// <summary>
    /// <see cref="LexerNs.Lexer.LeavesAQuoteUnpairedAtLineEnd"/> (plan-f92797 Design 4, R-FV, #2280): arm (b) of
    /// <c>RePairedCloser</c> reads each quote's ROLE in the text the lexer gives up on a close line. True iff a literal
    /// is open at the line's end, or text the walk cannot pair — the remainder after a <c>#</c> outside a literal, the
    /// text of an UNTERMINATED backtick name — holds a quote. A closed literal (the aborted literal's own delimiters)
    /// and a terminated backtick name contribute nothing; the own quote in a format spec keeps "either walk".
    /// </summary>
    [Theory]
    // the aborted literal's own delimiters pair: nothing is unpaired
    [InlineData("\"\\q\"", false)]
    [InlineData("'\\q'", false)]
    [InlineData("b\"\\q\"", false)]
    [InlineData("f\"{x!q}\"", false)]
    [InlineData("t\"{x!q}\"", false)]
    [InlineData("f\"}\"", false)]
    [InlineData("r\"\\\"", false)]                  // r"\" closes at its second quote: no escape in a raw literal
    [InlineData("\"\"\"abc\"\"\"", false)]
    [InlineData("$", false)]
    [InlineData("1__2", false)]
    // a literal open at the line's end: the orphan's closing quote, an unterminated short or triple string
    [InlineData("$'", true)]
    [InlineData("\"\\q\"'", true)]
    [InlineData("f\"}\"'", true)]
    [InlineData("\"abc", true)]
    [InlineData("\"a\\\"", true)]                   // "a\" — the escaped quote leaves the literal open
    [InlineData("\"\"\"abc", true)]
    [InlineData("f'{x}", true)]
    [InlineData("\"abc\nx = 1", true)]              // the close line's end is the first line break
    [InlineData("x\n'", false)]                     // a quote on a later line is not the close line's
    // a terminated backtick name is opaque; an unterminated one is text the walk cannot pair
    [InlineData("`it's`", false)]
    [InlineData("`it's` + 'z'", false)]
    [InlineData("`it's`'", true)]
    [InlineData("`it's", true)]
    [InlineData("`'", true)]
    [InlineData("`x'", true)]
    [InlineData("`x", false)]
    // the remainder after '#' outside a literal counts any quote, paired or not
    [InlineData("$  # it's", true)]
    [InlineData("$  # it's'", true)]
    [InlineData("$  # \"a\"", true)]
    [InlineData("$  # no quote", false)]
    [InlineData("\"#'\"", false)]                   // a '#' inside a closed string is text, not a comment
    [InlineData("f\"{x  # it's", true)]             // a comment in a field: the literal stays open past it
    // the literal's own quote in a format spec: either walk (#2276)
    [InlineData("f\"{x:\">}\"", true)]
    [InlineData("f\"{x:\">}\"'", true)]
    [InlineData("f\"{x:>}\"", false)]
    public void LeavesAQuoteUnpairedAtLineEnd(string text, bool expected)
    {
        LexerNs.Lexer.LeavesAQuoteUnpairedAtLineEnd(text).Should().Be(expected, text);
    }

    /// <summary>
    /// The complexity control: a line of 40 spec-quote aborts walks each start at most once (memoized), and its
    /// twin with a triple at the end still sees it.
    /// </summary>
    [Fact]
    public void HoldsALiteralSpanningLines_ManySpecQuoteAborts_StaysBounded()
    {
        const int aborts = 40;
        var line = "x = " + string.Concat(Enumerable.Repeat("f\"{x:\">}\" + ", aborts)) + "1";
        LexerNs.Lexer.HoldsALiteralSpanningLines(line, out var walks).Should().BeFalse();
        walks.Should().BeInRange(aborts, 2 * aborts, "two fresh walks per abort start, memoized — not 2^40 (a clock would read the runner's load, #2278)");
        LexerNs.Lexer.HoldsALiteralSpanningLines(line + " + \"\"\"", out walks).Should().BeTrue();
        walks.Should().BeInRange(1, 2 * aborts, "the opener is found on the first walk that reaches it");
    }
}
