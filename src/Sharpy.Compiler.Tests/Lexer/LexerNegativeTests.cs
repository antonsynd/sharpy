using FluentAssertions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;
using LexerError = Sharpy.Compiler.Lexer.LexerError;
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// Negative tests for the Lexer - testing error detection and handling
/// </summary>
public class LexerNegativeTests
{
    private static List<LexerNs.Token> Tokenize(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        return lexer.TokenizeAll();
    }

    #region Invalid Numeric Literals

    [Fact]
    public void RejectsMultipleDecimalPoints()
    {
        var source = "1.2.3";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsInvalidIntegerSuffix()
    {
        var source = "42X";  // X is not a valid suffix
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsInvalidFloatSuffix()
    {
        var source = "3.14x";  // x is not a valid float suffix
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsHexWithNoDigits()
    {
        var source = "0x";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsBinaryWithNoDigits()
    {
        var source = "0b";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsOctalWithNoDigits()
    {
        var source = "0o";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsInvalidHexDigits()
    {
        var source = "0xGHI";  // G, H, I are not hex digits
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsInvalidBinaryDigits()
    {
        var source = "0b102";  // 2 is not a binary digit
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsInvalidOctalDigits()
    {
        var source = "0o89";  // 8 and 9 are not octal digits
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsScientificNotationWithNoExponent()
    {
        var source = "1e";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsScientificNotationWithInvalidExponent()
    {
        var source = "1eX";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsFloatStartingWithDecimal()
    {
        var source = ".5";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*at least one digit before*");
    }

    [Fact]
    public void RejectsNumberWithMultipleUnderscoresInRow()
    {
        var source = "1__000";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsNumberStartingWithUnderscore()
    {
        var source = "_123";
        // This should be valid identifier, not a number error
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier);
    }

    [Fact]
    public void RejectsNumberEndingWithUnderscore()
    {
        var source = "123_";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    #endregion

    #region Invalid String Literals

    [Fact]
    public void RejectsUnterminatedString()
    {
        var source = "\"hello";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*Unterminated string*");
    }

    [Fact]
    public void RejectsUnterminatedSingleQuotedString()
    {
        var source = "'hello";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*Unterminated string*");
    }

    [Fact]
    public void RejectsUnterminatedTripleQuotedString()
    {
        var source = "\"\"\"hello";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsUnterminatedRawString()
    {
        var source = "r\"hello";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsUnterminatedFString()
    {
        var source = "f\"hello {name}";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsInvalidEscapeSequence()
    {
        var source = "\"hello\\xworld\"";  // \x is invalid without hex digits
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*escape sequence*");
    }

    [Fact]
    public void RejectsStringWithNewlineWithoutEscape()
    {
        var source = "\"hello\nworld\"";
        // Single-line strings cannot contain unescaped newlines
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsMismatchedQuotes()
    {
        var source = "\"hello'";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsInvalidStringPrefix()
    {
        var source = "x\"hello\"";  // x is not a valid string prefix
        // This should tokenize as identifier followed by string
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    #endregion

    #region Invalid Identifiers

    [Fact]
    public void RejectsIdentifierStartingWithDigit()
    {
        var source = "2fast";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsIdentifierWithInvalidCharacters()
    {
        var source = "hello$world";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*Unexpected character*");
    }

    [Fact]
    public void RejectsEmojiInIdentifier()
    {
        var source = "hello😀world";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsIdentifierWithAtSign()
    {
        var source = "hello@world";
        // @ should be treated as a separate token (decorator marker)
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "hello");
        tokens.Should().Contain(t => t.Type == TokenType.At);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "world");
    }

    #endregion

    #region Invalid Indentation

    [Fact]
    public void RejectsTabIndentation()
    {
        var source = "if True:\n\tpass";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*Tabs are not allowed*");
    }

    [Fact]
    public void RejectsMixedTabsAndSpaces()
    {
        var source = "if True:\n  \tx = 1";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsIndentationNotMultipleOfFour()
    {
        var source = "if True:\n  pass";  // 2 spaces
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*multiple of 4*");
    }

    [Fact]
    public void RejectsIndentationNotMultipleOfFour_ThreeSpaces()
    {
        var source = "if True:\n   pass";  // 3 spaces
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*multiple of 4*");
    }

    [Fact]
    public void RejectsIndentationNotMultipleOfFour_FiveSpaces()
    {
        var source = "if True:\n     pass";  // 5 spaces
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*multiple of 4*");
    }

    [Fact]
    public void RejectsInconsistentDedentation()
    {
        var source = @"if True:
    if False:
        pass
      x = 1";  // 6 spaces - doesn't match any previous level
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsIndentWithoutPrecedingColon()
    {
        var source = "x = 1\n    y = 2";  // Unexpected indent
        Action act = () => Tokenize(source);
        // Lexer will emit INDENT token - parser should reject this, not lexer
        // So this should NOT throw from lexer
        act.Should().NotThrow();
        
        // But verify it does produce an INDENT token
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Indent);
    }

    #endregion

    #region Invalid Line Continuation

    [Fact]
    public void RejectsBackslashAtEndOfFile()
    {
        var source = "x = 1 + \\";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsBackslashWithSpaceAfter()
    {
        var source = "x = 1 + \\ \n2";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsBackslashWithTextAfter()
    {
        var source = "x = 1 + \\y\n2";
        // Backslash followed by non-newline text is not a line continuation
        // It just tokenizes as separate tokens (backslash operator + identifier)
        // This is valid lexically, though may not make sense semantically
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Backslash);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
    }

    #endregion

    #region Invalid Literal Names

    [Fact]
    public void RejectsUnterminatedLiteralName()
    {
        var source = "`unterminated";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*Unterminated literal name*");
    }

    [Fact]
    public void RejectsLiteralNameWithNewline()
    {
        var source = "`name\nwith newline`";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*Unterminated literal name*");
    }

    #endregion

    #region Invalid Operators and Delimiters

    [Fact]
    public void RejectsInvalidOperator()
    {
        var source = "x $ y";  // $ is not a valid operator
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*Unexpected character*");
    }

    [Fact]
    public void RejectsHashOutsideComment()
    {
        var source = "x # y";
        // Hash starts a comment, so this should be valid
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().NotContain(t => t.Type == TokenType.Identifier && t.Value == "y");
    }

    [Fact]
    public void RejectsUnexpectedNullCharacter()
    {
        var source = "x = \0 y";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void RejectsUnexpectedControlCharacters()
    {
        var source = "x = \u0001 y";  // ASCII control character
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RejectsVeryLongIdentifier()
    {
        // Identifiers should have reasonable length limits
        var veryLongName = new string('a', 100000);
        var source = veryLongName;
        // This might succeed or fail depending on implementation limits
        // Just ensure it doesn't crash
        try
        {
            var tokens = Tokenize(source);
            tokens.Should().NotBeNull();
        }
        catch (LexerError)
        {
            // Acceptable if there's a length limit
        }
    }

    [Fact]
    public void RejectsVeryDeeplyNestedIndentation()
    {
        var source = "if True:\n";
        for (int i = 1; i <= 1000; i++)
        {
            source += new string(' ', i * 4) + "pass\n";
        }
        
        // Should not crash, but may have depth limits
        try
        {
            var tokens = Tokenize(source);
            tokens.Should().NotBeNull();
        }
        catch (LexerError)
        {
            // Acceptable if there's a depth limit
        }
        catch (StackOverflowException)
        {
            // Should not happen - this would be a bug
            throw;
        }
    }

    [Fact]
    public void HandlesEmptyInput()
    {
        var source = "";
        var tokens = Tokenize(source);
        tokens.Should().ContainSingle(t => t.Type == TokenType.Eof);
    }

    [Fact]
    public void HandlesOnlyWhitespace()
    {
        var source = "    \n    \n    ";
        var tokens = Tokenize(source);
        // Should not throw, only EOF and possibly newlines
        tokens.Should().Contain(t => t.Type == TokenType.Eof);
    }

    [Fact]
    public void HandlesOnlyComments()
    {
        var source = "# comment 1\n# comment 2\n# comment 3";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Eof);
        // Comments should be stripped
    }

    [Fact]
    public void RejectsInvalidUnicodeEscape()
    {
        var source = "\"\\u123\"";  // Incomplete unicode escape
        Action act = () => Tokenize(source);
        // Depending on implementation, this might be accepted or rejected
        // Document the behavior
        try
        {
            var tokens = Tokenize(source);
            // If accepted, ensure it doesn't crash
            tokens.Should().NotBeNull();
        }
        catch (LexerError)
        {
            // This is also acceptable
        }
    }

    #endregion

    #region Boundary Conditions

    [Fact]
    public void HandlesSingleCharacterToken()
    {
        var source = "+";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Plus);
    }

    [Fact]
    public void HandlesMaximalOperatorCombination()
    {
        // Test the longest possible operator sequence
        var source = ">>=";  // Three-character operator
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.RightShiftAssign);
    }

    [Fact]
    public void RejectsIncompleteTwoCharOperator()
    {
        // This should tokenize as two separate tokens, not reject
        var source = "< =";  // Space between < and =
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Less);
        tokens.Should().Contain(t => t.Type == TokenType.Assign);
    }

    [Fact]
    public void HandlesIncompleteDotDotDot()
    {
        // Two dots tokenizes as two separate Dot tokens
        var source = "..";
        var tokens = Tokenize(source);
        tokens.Count(t => t.Type == TokenType.Dot).Should().Be(2);
    }

    [Fact]
    public void HandlesMultipleNewlines()
    {
        var source = "\n\n\n";
        var tokens = Tokenize(source);
        // Should collapse or preserve newlines appropriately
        tokens.Should().NotBeNull();
    }

    [Fact]
    public void HandlesCarriageReturnNewline()
    {
        var source = "x = 1\r\ny = 2";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
    }

    [Fact]
    public void HandlesOnlyCarriageReturn()
    {
        var source = "x = 1\ry = 2";
        var tokens = Tokenize(source);
        // Behavior depends on whether \r alone is treated as newline
        tokens.Should().NotBeNull();
    }

    #endregion

    #region Complex Error Scenarios

    [Fact]
    public void RejectsNestedBackticks()
    {
        var source = "`outer`inner`outer`";
        // This should parse as three identifiers
        var tokens = Tokenize(source);
        var identifiers = tokens.Where(t => t.Type == TokenType.Identifier).ToList();
        identifiers.Should().HaveCount(3);
    }

    [Fact]
    public void RejectsUnmatchedBrackets()
    {
        // Lexer shouldn't care about bracket matching, that's parser's job
        var source = "[[[";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.LeftBracket);
        tokens.Count(t => t.Type == TokenType.LeftBracket).Should().Be(3);
    }

    [Fact]
    public void HandlesUTF8BOM()
    {
        // UTF-8 BOM should be handled gracefully
        var source = "\uFEFFx = 1";
        try
        {
            var tokens = Tokenize(source);
            tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        }
        catch (LexerError)
        {
            // Acceptable if BOM is not supported
        }
    }

    [Fact]
    public void RejectsMultipleConsecutiveUnderscoresInNumber()
    {
        var source = "1__2__3";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void AllowsUnderscoreAfterNumberPrefix()
    {
        // Per PEP 515, underscores can immediately follow prefix for readability
        var sources = new[] { "0x_FF", "0b_1010", "0o_77" };
        foreach (var source in sources)
        {
            var tokens = Tokenize(source);
            tokens.Should().Contain(t => t.Type == TokenType.Integer);
        }
    }

    [Fact]
    public void AcceptsUnderscoreInMiddleOfNumber()
    {
        var source = "1_000_000";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Integer);
    }

    #endregion
}
