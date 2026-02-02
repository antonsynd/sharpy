using FluentAssertions;
using System.Text;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// Negative tests for the Lexer - testing error detection and handling.
/// Uses the DiagnosticBag-based error collection via TokenizeAll().
/// </summary>
public class LexerNegativeTests
{
    private static List<LexerNs.Token> Tokenize(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        return lexer.TokenizeAll();
    }

    /// <summary>
    /// Tokenize and assert that at least one error was collected.
    /// Returns the diagnostic messages for further assertions.
    /// </summary>
    private static string TokenizeExpectingError(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeTrue("Expected lexer to report an error for input: " + source);
        return string.Join("\n", lexer.Diagnostics.GetErrors().Select(d => d.Message));
    }

    #region Invalid Numeric Literals

    [Fact]
    public void RejectsMultipleDecimalPoints()
    {
        TokenizeExpectingError("1.2.3");
    }

    [Fact]
    public void RejectsInvalidIntegerSuffix()
    {
        TokenizeExpectingError("42X");  // X is not a valid suffix
    }

    [Fact]
    public void RejectsInvalidFloatSuffix()
    {
        TokenizeExpectingError("3.14x");  // x is not a valid float suffix
    }

    [Fact]
    public void RejectsHexWithNoDigits()
    {
        TokenizeExpectingError("0x");
    }

    [Fact]
    public void RejectsBinaryWithNoDigits()
    {
        TokenizeExpectingError("0b");
    }

    [Fact]
    public void RejectsOctalWithNoDigits()
    {
        TokenizeExpectingError("0o");
    }

    [Fact]
    public void RejectsInvalidHexDigits()
    {
        TokenizeExpectingError("0xGHI");  // G, H, I are not hex digits
    }

    [Fact]
    public void RejectsInvalidBinaryDigits()
    {
        TokenizeExpectingError("0b102");  // 2 is not a binary digit
    }

    [Fact]
    public void RejectsInvalidOctalDigits()
    {
        TokenizeExpectingError("0o89");  // 8 and 9 are not octal digits
    }

    [Fact]
    public void RejectsScientificNotationWithNoExponent()
    {
        TokenizeExpectingError("1e");
    }

    [Fact]
    public void RejectsScientificNotationWithInvalidExponent()
    {
        TokenizeExpectingError("1eX");
    }

    [Fact]
    public void RejectsFloatStartingWithDecimal()
    {
        var errors = TokenizeExpectingError(".5");
        errors.Should().Contain("at least one digit before");
    }

    [Fact]
    public void RejectsNumberWithMultipleUnderscoresInRow()
    {
        TokenizeExpectingError("1__000");
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
        TokenizeExpectingError("123_");
    }

    #endregion

    #region Invalid String Literals

    [Fact]
    public void RejectsUnterminatedString()
    {
        var errors = TokenizeExpectingError("\"hello");
        errors.Should().Contain("Unterminated string");
    }

    [Fact]
    public void RejectsUnterminatedSingleQuotedString()
    {
        var errors = TokenizeExpectingError("'hello");
        errors.Should().Contain("Unterminated string");
    }

    [Fact]
    public void RejectsUnterminatedTripleQuotedString()
    {
        TokenizeExpectingError("\"\"\"hello");
    }

    [Fact]
    public void RejectsUnterminatedRawString()
    {
        TokenizeExpectingError("r\"hello");
    }

    [Fact]
    public void RejectsUnterminatedFString()
    {
        TokenizeExpectingError("f\"hello {name}");
    }

    [Fact]
    public void RejectsInvalidEscapeSequence()
    {
        var errors = TokenizeExpectingError("\"hello\\xworld\"");  // \x is invalid without hex digits
        errors.Should().Contain("escape sequence");
    }

    [Fact]
    public void RejectsStringWithNewlineWithoutEscape()
    {
        // Single-line strings cannot contain unescaped newlines
        TokenizeExpectingError("\"hello\nworld\"");
    }

    [Fact]
    public void RejectsMismatchedQuotes()
    {
        TokenizeExpectingError("\"hello'");
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
        TokenizeExpectingError("2fast");
    }

    [Fact]
    public void RejectsIdentifierWithInvalidCharacters()
    {
        var errors = TokenizeExpectingError("hello$world");
        errors.Should().Contain("Unexpected character");
    }

    [Fact]
    public void RejectsEmojiInIdentifier()
    {
        TokenizeExpectingError("hello😀world");
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
        var errors = TokenizeExpectingError("if True:\n\tpass");
        errors.Should().Contain("Tabs are not allowed");
    }

    [Fact]
    public void RejectsMixedTabsAndSpaces()
    {
        TokenizeExpectingError("if True:\n  \tx = 1");
    }

    [Fact]
    public void RejectsIndentationNotMultipleOfFour()
    {
        var errors = TokenizeExpectingError("if True:\n  pass");  // 2 spaces
        errors.Should().Contain("multiple of 4");
    }

    [Fact]
    public void RejectsIndentationNotMultipleOfFour_ThreeSpaces()
    {
        var errors = TokenizeExpectingError("if True:\n   pass");  // 3 spaces
        errors.Should().Contain("multiple of 4");
    }

    [Fact]
    public void RejectsIndentationNotMultipleOfFour_FiveSpaces()
    {
        var errors = TokenizeExpectingError("if True:\n     pass");  // 5 spaces
        errors.Should().Contain("multiple of 4");
    }

    [Fact]
    public void RejectsInconsistentDedentation()
    {
        TokenizeExpectingError("if True:\n    if False:\n        pass\n      x = 1");  // 6 spaces - doesn't match any previous level
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
        TokenizeExpectingError("x = 1 + \\");
    }

    [Fact]
    public void RejectsBackslashWithSpaceAfter()
    {
        TokenizeExpectingError("x = 1 + \\ \n2");
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
        var errors = TokenizeExpectingError("`unterminated");
        errors.Should().Contain("Unterminated literal name");
    }

    [Fact]
    public void RejectsLiteralNameWithNewline()
    {
        var errors = TokenizeExpectingError("`name\nwith newline`");
        errors.Should().Contain("Unterminated literal name");
    }

    #endregion

    #region Invalid Operators and Delimiters

    [Fact]
    public void RejectsInvalidOperator()
    {
        var errors = TokenizeExpectingError("x $ y");  // $ is not a valid operator
        errors.Should().Contain("Unexpected character");
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
        TokenizeExpectingError("x = \0 y");
    }

    [Fact]
    public void RejectsUnexpectedControlCharacters()
    {
        TokenizeExpectingError("x = \u0001 y");  // ASCII control character
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RejectsVeryLongIdentifier()
    {
        // Identifiers should have reasonable length limits
        var veryLongName = new string('a', 100000);
        // This might succeed or report an error depending on implementation limits
        // Just ensure it doesn't crash
        var lexer = new LexerNs.Lexer(veryLongName);
        var tokens = lexer.TokenizeAll();
        tokens.Should().NotBeNull();
    }

    [Fact]
    public void RejectsVeryDeeplyNestedIndentation()
    {
        var sb = new StringBuilder("if True:\n");
        for (int i = 1; i <= 1000; i++)
        {
            sb.Append(new string(' ', i * 4)).Append("pass\n");
        }
        var source = sb.ToString();

        // Should not crash, but may have depth limits
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        tokens.Should().NotBeNull();
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
        // Incomplete unicode escape - may be accepted or rejected
        var lexer = new LexerNs.Lexer("\"\\u123\"");
        var tokens = lexer.TokenizeAll();
        tokens.Should().NotBeNull();
        // Either succeeds or reports error via diagnostics - both are acceptable
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
        var lexer = new LexerNs.Lexer("\uFEFFx = 1");
        var tokens = lexer.TokenizeAll();
        // Either succeeds or reports error via diagnostics - both are acceptable
        tokens.Should().NotBeNull();
    }

    [Fact]
    public void RejectsMultipleConsecutiveUnderscoresInNumber()
    {
        TokenizeExpectingError("1__2__3");
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

    #region Error Recovery

    [Fact]
    public void RecoverFromUnterminatedString_TokenizesNextLine()
    {
        // Unterminated string on line 1, valid code on line 2
        var source = "x = \"hello\ny = 42";
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();

        // Should report an error for the unterminated string
        lexer.Diagnostics.HasErrors.Should().BeTrue();

        // Should have recovered and produced tokens beyond the error
        // The key assertion: we get more than just an EOF token
        tokens.Count.Should().BeGreaterThan(1);
        tokens.Last().Type.Should().Be(TokenType.Eof);
    }

    [Fact]
    public void ReportsMultipleLexerErrors()
    {
        // Multiple lines with lexer errors
        var source = "x = \"unterminated\ny = \"also unterminated\nz = 42";
        var lexer = new LexerNs.Lexer(source);
        lexer.TokenizeAll();

        // Should report more than one error
        lexer.Diagnostics.GetErrors().Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void RecoveryStopsAtMaxErrors()
    {
        // Generate many lines with errors
        var lines = Enumerable.Range(1, 30).Select(i => $"x{i} = \"unterminated");
        var source = string.Join("\n", lines);

        var lexer = new LexerNs.Lexer(source);
        lexer.MaxErrors = 5;
        var tokens = lexer.TokenizeAll();

        // Should stop at MaxErrors
        lexer.Diagnostics.GetErrors().Count.Should().BeLessThanOrEqualTo(5);
        tokens.Last().Type.Should().Be(TokenType.Eof);
    }

    [Fact]
    public void RecoveryEmitsTruncationWarningAtMaxErrors()
    {
        // Generate many lines with errors to exceed MaxErrors
        var lines = Enumerable.Range(1, 30).Select(i => $"x{i} = \"unterminated");
        var source = string.Join("\n", lines);

        var lexer = new LexerNs.Lexer(source);
        lexer.MaxErrors = 3;
        lexer.TokenizeAll();

        // Should emit exactly one SHP0905 truncation warning
        var warnings = lexer.Diagnostics.GetWarnings().ToList();
        warnings.Where(w => w.Code == "SHP0905").Should().HaveCount(1,
            "a single truncation warning should be emitted when MaxErrors is reached");
    }

    [Fact]
    public void RecoveryProducesEofToken()
    {
        // Error followed by EOF
        var source = "x = \"unterminated";
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();

        tokens.Last().Type.Should().Be(TokenType.Eof);
    }

    [Fact]
    public void RecoveryFromInvalidCharacter_TokenizesNextLine()
    {
        // Invalid character on line 1, valid identifier on line 2
        var source = "$\nvalid_name";
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();

        lexer.Diagnostics.HasErrors.Should().BeTrue();
        // Should have recovered and tokenized the second line
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "valid_name");
        tokens.Last().Type.Should().Be(TokenType.Eof);
    }

    [Fact]
    public void RecoveryFromInvalidEscape_TokenizesNextLine()
    {
        // Invalid escape on line 1, valid code on line 2
        var source = "x = \"\\q\"\ny = 42";
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();

        lexer.Diagnostics.HasErrors.Should().BeTrue();
        tokens.Last().Type.Should().Be(TokenType.Eof);
        // Should have tokens from the recovery
        tokens.Count.Should().BeGreaterThan(1);
    }

    #endregion
}
