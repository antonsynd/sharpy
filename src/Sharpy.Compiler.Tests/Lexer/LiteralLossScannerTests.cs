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
    // an f-/t-string whose replacement field is left open on its line
    [InlineData("x = f\"{', '.join(", true)]
    [InlineData("x = f\"{a", true)]
    [InlineData("x = t\"{a", true)]
    [InlineData("x = df\"{a", true)]
    [InlineData("x = f\"{a}\" + \"b", false)]               // a closed hole, then an unterminated short string
    // any other unterminated short string ends at its line
    [InlineData("x = r\"abc", false)]
    [InlineData("x = d\"abc", false)]
    [InlineData("x = b\"abc", false)]
    [InlineData("x = xf\"{a", false)]                        // xf is an identifier: a plain string follows
    // the known limit, pinned so the choice is recorded (#2271): the opener is swallowed by a short string
    [InlineData("x = 'abc \"\"\"", false)]
    public void HoldsALiteralSpanningLines(string text, bool expected)
    {
        LexerNs.Lexer.HoldsALiteralSpanningLines(text).Should().Be(expected, text);
    }
}
