using FluentAssertions;
using Sharpy.TestInfrastructure.Lexing;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// The position instrument (<see cref="TokenPositionReconstruction"/>, #2300 C2) is itself a guard, so each of its
/// rules is shown to fire: a clean lex reconstructs (the baseline), and one hand-broken token per rule yields that
/// rule's line. A rule that never fires here would let the corpus twin pass vacuously.
/// </summary>
public class TokenPositionReconstructionTests
{
    private const string Mixed =
        "if a >> b:\n    y = `na me` + 0X_FF + .5e-3 + 1_000\n    z = f\"{x!r:>4}{y=}\" + dr'''\n  k\n  ''' + b\"q\"  # c\nw = \\\n    1\n";

    private static List<LexerNs.Token> Lex(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeFalse("the baseline must lex clean: {0}",
            string.Join("; ", lexer.Diagnostics.GetErrors().Select(d => $"{d.Code}@{d.Line}:{d.Column}")));
        return tokens;
    }

    private static List<string> Violations(string source, List<LexerNs.Token> tokens) =>
        TokenPositionReconstruction.Violations(source, tokens).ToList();

    private static List<LexerNs.Token> Replace(List<LexerNs.Token> tokens, Func<LexerNs.Token, bool> which,
        Func<LexerNs.Token, LexerNs.Token> change)
    {
        var index = tokens.FindIndex(t => which(t));
        index.Should().BeGreaterThanOrEqualTo(0, "the hand-broken token must exist in the baseline");
        var copy = tokens.ToList();
        copy[index] = change(copy[index]);
        return copy;
    }

    [Theory]
    [InlineData("", new[] { 0 })]
    [InlineData("a", new[] { 0 })]
    [InlineData("a\n", new[] { 0, 2 })]
    [InlineData("a\nb\r\nc\rd", new[] { 0, 2, 5, 7 })]
    [InlineData("\r\r\n\n", new[] { 0, 1, 3, 4 })]
    public void LineStarts_EveryBreakKindEndsALine(string source, int[] expected) =>
        TokenPositionReconstruction.LineStarts(source).Should().Equal(expected);

    [Theory]
    [InlineData("lf")]
    [InlineData("crlf")]
    [InlineData("cr")]
    [InlineData("no final break")]
    public void CleanLex_Reconstructs(string twin)
    {
        var source = twin switch
        {
            "crlf" => Mixed.Replace("\n", "\r\n", StringComparison.Ordinal),
            "cr" => Mixed.Replace('\n', '\r'),
            "no final break" => Mixed.TrimEnd('\n'),
            _ => Mixed,
        };
        var tokens = Lex(source);
        tokens.Should().Contain(t => t.Type == TokenType.Indent).And.Contain(t => t.Type == TokenType.Dedent)
            .And.Contain(t => t.Type == TokenType.FStringConversion).And.Contain(t => t.Type == TokenType.FStringSelfDoc)
            .And.Contain(t => t.Type == TokenType.FStringFormatSpec).And.Contain(t => t.IsBacktickEscaped)
            .And.Contain(t => t.Type == TokenType.RawString).And.Contain(t => t.Type == TokenType.ByteString);

        Violations(source, tokens).Should().BeEmpty();
    }

    [Fact]
    public void R1_AColumnOneShortAfterAConstruct_IsSeenOnTheFollowingToken()
    {
        var tokens = Lex(Mixed);
        var broken = Replace(tokens, t => t.Value == "b" && t.Line == 1, t => t with { Column = t.Column - 1 });

        Violations(Mixed, broken).Should().ContainSingle().Which.Should().StartWith("R1 Identifier 'b'");
    }

    [Fact]
    public void R1_ALineOffByOne_IsSeen()
    {
        var tokens = Lex(Mixed);
        var broken = Replace(tokens, t => t.Value == "w", t => t with { Line = t.Line + 1 });

        Violations(Mixed, broken).Should().Contain(v => v.StartsWith("R1 Identifier 'w'", StringComparison.Ordinal));
    }

    [Fact]
    public void R1_AColumnRunPastItsLinesEnd_IsSeen()
    {
        // L1:C3 sums to offset 2, which is where line 2 starts: right offset, wrong line (a break read without _line++).
        const string source = "x\ny\n";
        var tokens = Lex(source);
        var broken = Replace(tokens, t => t.Value == "y", t => t with { Line = 1, Column = 3 });

        Violations(source, broken).Should().ContainSingle().Which.Should().StartWith("R1 Identifier 'y'").And.Contain("is not on line 1");
    }

    [Fact]
    public void R1_ATokenWithoutAPosition_IsSeen()
    {
        var tokens = Lex(Mixed);
        var broken = Replace(tokens, t => t.Value == "w", t => t with { Position = -1 });

        Violations(Mixed, broken).Should().Contain(v => v.StartsWith("R1 Identifier 'w'", StringComparison.Ordinal) && v.EndsWith("no position", StringComparison.Ordinal));
    }

    [Fact]
    public void R1_AnIndentWithTextBeforeItsPosition_IsSeen()
    {
        // The Indent exemption (column 1, position after the indentation) admits spaces only.
        var tokens = Lex(Mixed);
        var broken = Replace(tokens, t => t.Type == TokenType.Indent, t => t with { Position = t.Position + 1 });

        Violations(Mixed, broken).Should().Contain(v => v.StartsWith("R1 Indent", StringComparison.Ordinal));
    }

    [Fact]
    public void R2_ASpanPastTheSourceEnd_IsSeen()
    {
        const string source = "x = \"abc\"";
        var tokens = Lex(source);
        var broken = Replace(tokens, t => t.Type == TokenType.String, t => t with { SourceLength = t.SourceLength + 3 });

        Violations(source, broken).Should().Contain(v => v.StartsWith("R2 String", StringComparison.Ordinal));
    }

    [Fact]
    public void R3_AVerbatimTokenWhoseSliceIsNotItsValue_IsSeen()
    {
        var tokens = Lex(Mixed);
        var broken = Replace(tokens, t => t.Type == TokenType.RightShift, t => t with { Value = ">=" });

        Violations(Mixed, broken).Should().Contain(v => v.StartsWith("R3 RightShift", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("0xFF", "0X_FF")]
    [InlineData("0.5e-3", ".5e-3")]
    [InlineData("1000", "1_000")]
    public void R3_ANumberIsReadThroughItsNormalization(string value, string spelling)
    {
        var source = "x = " + spelling;
        var tokens = Lex(source);
        tokens.Should().Contain(t => t.Value == value, "the lexer normalizes {0} to {1}", spelling, value);
        Violations(source, tokens).Should().BeEmpty();

        var broken = Replace(tokens, t => t.Value == value, t => t with { Value = value + "1" });
        Violations(source, broken).Should().Contain(v => v.StartsWith("R3 ", StringComparison.Ordinal) && v.Contains("normalized slice", StringComparison.Ordinal));
    }

    [Fact]
    public void R4_ABacktickNameWithoutItsSourceLength_IsSeen()
    {
        // The ReadLiteralName shape: SourceLength dropped → Length = Value.Length, two short.
        var tokens = Lex(Mixed);
        var broken = Replace(tokens, t => t.IsBacktickEscaped, t => t with { SourceLength = null });

        Violations(Mixed, broken).Should().Contain(v => v.StartsWith("R4 Identifier 'na me'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(TokenType.String, "x = \"abc\"")]
    [InlineData(TokenType.RawString, "x = dr'''\n  k\n  '''")]
    [InlineData(TokenType.ByteString, "x = b\"q\"")]
    public void R4_AStringSpanThatStopsShortOfItsCloser_IsSeen(TokenType type, string source)
    {
        var tokens = Lex(source);
        var broken = Replace(tokens, t => t.Type == type, t => t with { SourceLength = t.SourceLength - 1 });

        Violations(source, broken).Should().Contain(v => v.StartsWith("R4 " + type, StringComparison.Ordinal));
    }

    [Fact]
    public void R4_AConversionNotAtItsBang_IsSeen()
    {
        var tokens = Lex(Mixed);
        var broken = Replace(tokens, t => t.Type == TokenType.FStringConversion, t => t with { Position = t.Position + 1, Column = t.Column + 1 });

        Violations(Mixed, broken).Should().Contain(v => v.StartsWith("R4 FStringConversion", StringComparison.Ordinal));
    }

    [Fact]
    public void R4_ASelfDocSpanHoldingMoreThanTheEquals_IsSeen()
    {
        var tokens = Lex(Mixed);
        var broken = Replace(tokens, t => t.Type == TokenType.FStringSelfDoc, t => t with { SourceLength = t.SourceLength + 1 });

        Violations(Mixed, broken).Should().Contain(v => v.StartsWith("R4 FStringSelfDoc", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("lf")]
    [InlineData("crlf")]
    public void R5_ANewlineNotAtALineBreak_IsSeen(string twin)
    {
        var source = twin == "crlf" ? "x = 1\r\ny = 2\r\n" : "x = 1\ny = 2\n";
        var tokens = Lex(source);
        var broken = Replace(tokens, t => t.Type == TokenType.Newline, t => t with { Position = t.Position - 1, Column = t.Column - 1 });

        Violations(source, broken).Should().Contain(v => v.StartsWith("R5 Newline", StringComparison.Ordinal));
    }

    [Fact]
    public void R5_ANewlineWhoseNextLineStartsElsewhere_IsSeen()
    {
        // A CRLF break read as one character: the slice is a break, but line 2 starts after "\r\n".
        const string source = "x = 1\r\ny = 2\r\n";
        var tokens = Lex(source);
        var broken = Replace(tokens, t => t.Type == TokenType.Newline, t => t with { Line = 2 });

        Violations(source, broken).Should().Contain(v => v.StartsWith("R5 Newline", StringComparison.Ordinal));
    }

    [Fact]
    public void R6_AnEofShortOfTheSourceEnd_IsSeen()
    {
        const string source = "x = 1";
        var tokens = Lex(source);
        var broken = Replace(tokens, t => t.Type == TokenType.Eof, t => t with { Position = t.Position - 1, Column = t.Column - 1 });

        Violations(source, broken).Should().Contain(v => v.StartsWith("R6 Eof", StringComparison.Ordinal));
        TokenPositionReconstruction.Violations(source, tokens.Take(tokens.Count - 1).ToList()).Should()
            .Contain("R6 the token list does not end with Eof");
    }

    [Fact]
    public void R7_TextBetweenTokensThatIsNotTrivia_IsSeen()
    {
        const string source = "x = a + b";
        var tokens = Lex(source);
        var dropped = tokens.Where(t => t.Type != TokenType.Plus).ToList();

        Violations(source, dropped).Should().ContainSingle().Which.Should().StartWith("R7 gap");
    }

    [Fact]
    public void R7_OverlappingTokens_AreSeen()
    {
        const string source = "x = ab + c";
        var tokens = Lex(source);
        var broken = Replace(tokens, t => t.Value == "ab", t => t with { Value = "ab +" });

        Violations(source, broken).Should().Contain(v => v.StartsWith("R7 Plus", StringComparison.Ordinal) && v.Contains("overlaps", StringComparison.Ordinal));
    }

    [Fact]
    public void R7_DroppedTextIsExemptOnlyWhereRecoveryResumed()
    {
        // `$` aborts the first line; recovery resumes at line 2. That gap is dropped text, the rest is not.
        const string source = "x = $ junk\ny = 2\n";
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeTrue();
        lexer.RecoveryResumes.Should().ContainSingle();

        TokenPositionReconstruction.Violations(source, lexer, tokens).Should().BeEmpty();
        TokenPositionReconstruction.Violations(source, tokens).Should().ContainSingle().Which.Should().StartWith("R7 gap");
    }
}
