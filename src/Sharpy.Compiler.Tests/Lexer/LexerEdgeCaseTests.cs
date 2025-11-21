using FluentAssertions;
using System.Text;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;
using LexerError = Sharpy.Compiler.Lexer.LexerError;
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// Additional edge case tests for the Lexer beyond numeric literals
/// </summary>
public class LexerEdgeCaseTests
{
    private static List<LexerNs.Token> Tokenize(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        return lexer.TokenizeAll();
    }

    #region String Edge Cases

    [Fact]
    public void HandlesEmptyString()
    {
        var source = "\"\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesEmptySingleQuotedString()
    {
        var source = "''";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesEmptyTripleQuotedString()
    {
        var source = "\"\"\"\"\"\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesStringWithOnlyWhitespace()
    {
        var source = "\"   \"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesStringWithAllEscapeSequences()
    {
        var source = "\"\\n\\r\\t\\\\\\\"\\'\\/\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesUnicodeEscapeInString()
    {
        var source = "\"\\u0041\\u0042\\u0043\"";  // ABC
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesHexEscapeInString()
    {
        var source = "\"\\x41\\x42\\x43\"";  // ABC
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesOctalEscapeInString()
    {
        var source = "\"\\101\\102\\103\"";  // ABC
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact(Skip = "Unimplemented: Raw string prefix not yet supported")]
    public void HandlesRawStringWithBackslashes()
    {
        var source = "r\"C:\\path\\to\\file\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesVeryLongString()
    {
        var longContent = new string('a', 10000);
        var source = $"\"{longContent}\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesMultilineTripleQuotedString()
    {
        var source = @"
x = """"""
Line 1
Line 2
Line 3
""""""
";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact(Skip = "Unimplemented: F-string prefix not yet supported")]
    public void HandlesFStringWithNoInterpolation()
    {
        var source = "f\"plain text\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact(Skip = "Unimplemented: F-string interpolation not yet supported")]
    public void HandlesFStringWithSimpleInterpolation()
    {
        var source = "f\"value is {x}\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact(Skip = "Unimplemented: F-string complex expressions not yet supported")]
    public void HandlesFStringWithComplexExpression()
    {
        var source = "f\"result: {x + y * 2}\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesNestedQuotesInString()
    {
        var source = "\"He said 'hello'\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesSingleQuoteStringWithDoubleQuotes()
    {
        var source = "'She said \"hi\"'";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    #endregion

    #region Identifier Edge Cases

    [Fact]
    public void HandlesSingleLetterIdentifier()
    {
        var source = "x";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
    }

    [Fact]
    public void HandlesVeryLongIdentifier()
    {
        var longName = new string('a', 1000);
        var source = longName;
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier);
    }

    [Fact]
    public void HandlesIdentifierWithNumbers()
    {
        var source = "var123abc456";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "var123abc456");
    }

    [Fact]
    public void HandlesIdentifierWithUnderscores()
    {
        var source = "_private_var_123_";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier);
    }

    [Fact]
    public void HandlesDoubleUnderscoreIdentifier()
    {
        var source = "__private__";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "__private__");
    }

    [Fact]
    public void HandlesSingleUnderscore()
    {
        var source = "_";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "_");
    }

    [Fact]
    public void HandlesDoubleUnderscore()
    {
        var source = "__";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "__");
    }

    [Fact]
    public void HandlesAllCapsIdentifier()
    {
        var source = "MAX_SIZE";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "MAX_SIZE");
    }

    [Fact]
    public void HandlesMixedCaseIdentifier()
    {
        var source = "MyClassName";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "MyClassName");
    }

    #endregion

    #region Operator Edge Cases

    [Fact]
    public void HandlesAllComparisonOperators()
    {
        var source = "< > <= >= == !=";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Less);
        tokens.Should().Contain(t => t.Type == TokenType.Greater);
        tokens.Should().Contain(t => t.Type == TokenType.LessEqual);
        tokens.Should().Contain(t => t.Type == TokenType.GreaterEqual);
        tokens.Should().Contain(t => t.Type == TokenType.Equal);
        tokens.Should().Contain(t => t.Type == TokenType.NotEqual);
    }

    [Fact]
    public void HandlesAllArithmeticOperators()
    {
        var source = "+ - * / // % **";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Plus);
        tokens.Should().Contain(t => t.Type == TokenType.Minus);
        tokens.Should().Contain(t => t.Type == TokenType.Star);
        tokens.Should().Contain(t => t.Type == TokenType.Slash);
        tokens.Should().Contain(t => t.Type == TokenType.DoubleSlash);
        tokens.Should().Contain(t => t.Type == TokenType.Percent);
        tokens.Should().Contain(t => t.Type == TokenType.DoubleStar);
    }

    [Fact]
    public void HandlesAllBitwiseOperators()
    {
        var source = "& | ^ ~ << >>";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Ampersand);
        tokens.Should().Contain(t => t.Type == TokenType.Pipe);
        tokens.Should().Contain(t => t.Type == TokenType.Caret);
        tokens.Should().Contain(t => t.Type == TokenType.Tilde);
        tokens.Should().Contain(t => t.Type == TokenType.LeftShift);
        tokens.Should().Contain(t => t.Type == TokenType.RightShift);
    }

    [Fact]
    public void HandlesAllAssignmentOperators()
    {
        var source = "= += -= *= /= //= %= **= &= |= ^= <<= >>=";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Assign);
        tokens.Should().Contain(t => t.Type == TokenType.PlusAssign);
        tokens.Should().Contain(t => t.Type == TokenType.MinusAssign);
        tokens.Should().Contain(t => t.Type == TokenType.StarAssign);
        tokens.Should().Contain(t => t.Type == TokenType.SlashAssign);
    }

    [Fact]
    public void HandlesColonInDifferentContexts()
    {
        var source = "if True: pass\nx: int = 5\n{\"key\": \"value\"}";
        var tokens = Tokenize(source);
        tokens.Count(t => t.Type == TokenType.Colon).Should().BeGreaterThan(0);
    }

    [Fact]
    public void HandlesDotOperator()
    {
        var source = "obj.method";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Dot);
    }

    [Fact]
    public void HandlesEllipsis()
    {
        var source = "...";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Ellipsis);
    }

    [Fact]
    public void HandlesSemicolon()
    {
        var source = "x = 1; y = 2";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Semicolon);
    }

    #endregion

    #region Delimiter Edge Cases

    [Fact]
    public void HandlesAllBracketTypes()
    {
        var source = "() [] {}";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.LeftParen);
        tokens.Should().Contain(t => t.Type == TokenType.RightParen);
        tokens.Should().Contain(t => t.Type == TokenType.LeftBracket);
        tokens.Should().Contain(t => t.Type == TokenType.RightBracket);
        tokens.Should().Contain(t => t.Type == TokenType.LeftBrace);
        tokens.Should().Contain(t => t.Type == TokenType.RightBrace);
    }

    [Fact]
    public void HandlesNestedBrackets()
    {
        var source = "[[{()}]]";
        var tokens = Tokenize(source);
        tokens.Count(t => t.Type == TokenType.LeftBracket).Should().Be(2);
        tokens.Count(t => t.Type == TokenType.RightBracket).Should().Be(2);
    }

    [Fact]
    public void HandlesCommas()
    {
        var source = "a, b, c, d";
        var tokens = Tokenize(source);
        tokens.Count(t => t.Type == TokenType.Comma).Should().Be(3);
    }

    #endregion

    #region Whitespace and Indentation Edge Cases

    [Fact]
    public void HandlesMultipleSpaces()
    {
        var source = "x     =     5";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Assign);
    }

    [Fact]
    public void HandlesConsistentIndentation()
    {
        var source = @"
if True:
    x = 1
    y = 2
    z = 3
";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Indent);
        tokens.Should().Contain(t => t.Type == TokenType.Dedent);
    }

    [Fact]
    public void HandlesMultipleIndentLevels()
    {
        var source = @"
if True:
    if True:
        if True:
            pass
";
        var tokens = Tokenize(source);
        tokens.Count(t => t.Type == TokenType.Indent).Should().Be(3);
    }

    [Fact]
    public void HandlesBlankLines()
    {
        var source = @"
x = 1

y = 2


z = 3
";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "z");
    }

    [Fact]
    public void HandlesBlankLinesWithWhitespace()
    {
        var source = "x = 1\n    \ny = 2";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
    }

    #endregion

    #region Comment Edge Cases

    [Fact]
    public void HandlesCommentAtEndOfLine()
    {
        var source = "x = 5  # comment";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().NotContain(t => t.Value == "comment");
    }

    [Fact]
    public void HandlesCommentOnOwnLine()
    {
        var source = "# comment\nx = 5";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
    }

    [Fact]
    public void HandlesEmptyComment()
    {
        var source = "x = 5  #";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
    }

    [Fact]
    public void HandlesCommentWithSpecialCharacters()
    {
        var source = "x = 5  # !@#$%^&*()_+-=[]{}|;':\",./<>?";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
    }

    [Fact]
    public void HandlesMultipleConsecutiveCommentsOnSeparateLines()
    {
        var source = @"
# Comment 1
# Comment 2
# Comment 3
x = 5
";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
    }

    #endregion

    #region Keyword Edge Cases

    [Fact]
    public void DistinguishesKeywordsFromIdentifiers()
    {
        var source = "if_var = if";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "if_var");
        tokens.Should().Contain(t => t.Type == TokenType.If);
    }

    [Fact(Skip = "Implementation: Some keywords may not be fully recognized")]
    public void HandlesAllKeywords()
    {
        var keywords = new[]
        {
            "if", "else", "elif", "while", "for", "in", "def", "class",
            "return", "break", "continue", "pass", "import", "from",
            "as", "try", "except", "finally", "raise", "with", "assert",
            "True", "False", "None", "and", "or", "not", "is", "lambda"
        };

        foreach (var keyword in keywords)
        {
            var source = $"{keyword} x";
            var tokens = Tokenize(source);
            tokens.Should().NotContain(t => t.Type == TokenType.Identifier && t.Value == keyword);
        }
    }

    #endregion

    #region Literal Name Edge Cases

    [Fact]
    public void HandlesSimpleLiteralName()
    {
        var source = "`name`";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier);
    }

    [Fact]
    public void HandlesLiteralNameWithSpaces()
    {
        var source = "`name with spaces`";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier);
    }

    [Fact]
    public void HandlesLiteralNameWithSpecialChars()
    {
        var source = "`name-with-dashes`";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier);
    }

    [Fact]
    public void HandlesEmptyLiteralName()
    {
        var source = "``";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier);
    }

    #endregion

    #region Line Continuation Edge Cases

    [Fact]
    public void HandlesLineContinuationInExpression()
    {
        var source = "x = 1 + \\\n    2";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
    }

    [Fact]
    public void HandlesMultipleLineContinuations()
    {
        var source = "x = 1 + \\\n    2 + \\\n    3";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Plus);
    }

    [Fact]
    public void HandlesImplicitLineContinuationInParentheses()
    {
        var source = "x = (\n    1 + 2\n)";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
    }

    [Fact]
    public void HandlesImplicitLineContinuationInBrackets()
    {
        var source = "x = [\n    1,\n    2,\n    3\n]";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.LeftBracket);
    }

    [Fact]
    public void HandlesImplicitLineContinuationInBraces()
    {
        var source = "x = {\n    'a': 1,\n    'b': 2\n}";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.LeftBrace);
    }

    #endregion

    #region Token Sequences

    [Fact]
    public void HandlesAdjacentOperators()
    {
        var source = "x=1+2*3";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Assign);
        tokens.Should().Contain(t => t.Type == TokenType.Plus);
        tokens.Should().Contain(t => t.Type == TokenType.Star);
    }

    [Fact]
    public void HandlesOperatorWithoutSpaces()
    {
        var source = "x=y+z";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Assign);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
    }

    [Fact]
    public void HandlesConsecutiveCommas()
    {
        var source = "[1,,2]";
        var tokens = Tokenize(source);
        tokens.Count(t => t.Type == TokenType.Comma).Should().Be(2);
    }

    #endregion

    #region Special Numeric Cases (non-literal)

    [Fact]
    public void HandlesNumberFollowedByDot()
    {
        var source = "42.method()";
        var tokens = Tokenize(source);
        // This should tokenize properly
        tokens.Should().NotBeEmpty();
    }

    [Fact]
    public void HandlesNumberWithUnderscore()
    {
        var source = "1_000_000";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Integer);
    }

    [Fact]
    public void HandlesHexNumber()
    {
        var source = "0xDEADBEEF";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Integer);
    }

    [Fact]
    public void HandlesBinaryNumber()
    {
        var source = "0b10101010";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Integer);
    }

    [Fact]
    public void HandlesOctalNumber()
    {
        var source = "0o755";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Integer);
    }

    #endregion

    #region Edge Cases with Newlines

    [Fact]
    public void HandlesWindowsLineEndings()
    {
        var source = "x = 1\r\ny = 2";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
    }

    [Fact]
    public void HandlesUnixLineEndings()
    {
        var source = "x = 1\ny = 2";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
    }

    [Fact]
    public void HandlesMacLineEndings()
    {
        var source = "x = 1\ry = 2";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
    }

    [Fact]
    public void HandlesMixedLineEndings()
    {
        var source = "x = 1\ny = 2\r\nz = 3";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "z");
    }

    #endregion

    #region At Symbol (@) for Decorators

    [Fact]
    public void HandlesAtSymbol()
    {
        var source = "@decorator";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.At);
    }

    [Fact]
    public void HandlesMultipleDecorators()
    {
        var source = "@decorator1\n@decorator2\ndef foo(): pass";
        var tokens = Tokenize(source);
        tokens.Count(t => t.Type == TokenType.At).Should().Be(2);
    }

    #endregion

    #region Arrow (->) for Return Type Annotations

    [Fact]
    public void HandlesArrowOperator()
    {
        var source = "def foo() -> int:";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Arrow);
    }

    #endregion

    #region Stress Tests

    [Fact]
    public void HandlesVeryLongLine()
    {
        var longLine = "x = " + string.Join(" + ", Enumerable.Range(1, 1000));
        var tokens = Tokenize(longLine);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
    }

    [Fact]
    public void HandlesManyTokensOnOneLine()
    {
        var source = string.Join(" ", Enumerable.Range(1, 100).Select(i => $"x{i}"));
        var tokens = Tokenize(source);
        tokens.Count(t => t.Type == TokenType.Identifier).Should().Be(100);
    }

    [Fact]
    public void HandlesManyBlankLines()
    {
        var blankLines = string.Join("\n", Enumerable.Repeat("", 100));
        var source = $"x = 1{blankLines}y = 2";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
    }

    #endregion

    #region Unicode and Special Characters

    [Fact]
    public void AcceptsUnicodeInComments()
    {
        var source = "x = 1  # 你好世界 😀";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
    }

    [Fact]
    public void AcceptsUnicodeInStrings()
    {
        var source = "x = \"你好世界\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesZeroWidthCharacters()
    {
        // Zero-width space (U+200B)
        var source = "x\u200B = 1";
        // Behavior depends on whether zero-width chars are allowed
        try
        {
            var tokens = Tokenize(source);
            tokens.Should().NotBeNull();
        }
        catch (LexerError)
        {
            // Also acceptable
        }
    }

    #endregion

    #region Escape Sequence Edge Cases

    [Fact]
    public void HandlesMixedEscapeSequences()
    {
        var source = "\"Line1\\nHex\\x20Unicode\\u0021Octal\\101\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesMaxOctalValue()
    {
        var source = "\"\\377\"";  // Max octal value (255)
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesMinOctalValue()
    {
        var source = "\"\\0\"";  // Min octal value
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesSingleDigitOctal()
    {
        var source = "\"\\7\"";  // Single digit octal
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesTwoDigitOctal()
    {
        var source = "\"\\77\"";  // Two digit octal
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesUppercaseUnicodeEscape()
    {
        var source = "\"\\U00000041\"";  // 8-digit Unicode escape for 'A'
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesZeroUnicodeEscape()
    {
        var source = "\"\\u0000\"";  // Null character via Unicode
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesMaxBMPUnicodeEscape()
    {
        var source = "\"\\uFFFF\"";  // Max Basic Multilingual Plane value
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesLowercaseHexEscape()
    {
        var source = "\"\\x41\\x42\\x43\"";  // ABC with lowercase x
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesBackspaceEscape()
    {
        var source = "\"\\b\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesFormFeedEscape()
    {
        var source = "\"\\f\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesVerticalTabEscape()
    {
        var source = "\"\\v\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesAlertEscape()
    {
        var source = "\"\\a\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesEscapeSequencesInTripleQuotedString()
    {
        var source = "\"\"\"Line1\\nLine2\\tTabbed\"\"\"";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void HandlesConsecutiveEscapes()
    {
        var source = "\"\\n\\n\\n\"";  // Multiple consecutive newlines
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void ThrowsOnInvalidHexEscape()
    {
        var source = "\"\\xGG\"";  // Invalid hex digits
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void ThrowsOnIncompletehexEscape()
    {
        var source = "\"\\x4\"";  // Only one hex digit
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void ThrowsOnIncompleteUnicodeEscape()
    {
        var source = "\"\\u004\"";  // Only 3 hex digits
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void ThrowsOnInvalidUnicodeEscape()
    {
        var source = "\"\\u00GG\"";  // Invalid hex digits in Unicode
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void ThrowsOnOctalValueTooLarge()
    {
        var source = "\"\\400\"";  // 256 in octal, exceeds max (255)
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void ThrowsOnInvalidEscapeCharacter()
    {
        var source = "\"\\q\"";  // Invalid escape sequence
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void ThrowsOnUnterminatedStringWithEscape()
    {
        var source = "\"hello\\n";  // Unterminated string
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    #endregion
}
