using FluentAssertions;
using System.Text;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;
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

    private static string TokenizeExpectingError(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        lexer.TokenizeAll();
        Assert.True(lexer.Diagnostics.HasErrors, "Expected lexer to report an error for input: " + source);
        return string.Join("\n", lexer.Diagnostics.GetErrors().Select(d => d.Message));
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

    [Fact]
    public void HandlesRawStringWithBackslashes()
    {
        var source = "r\"C:\\path\\to\\file\"";
        var tokens = Tokenize(source);
        // Raw strings should produce a RawString token that preserves backslashes
        tokens.Should().Contain(t => t.Type == TokenType.RawString && t.Value == "C:\\path\\to\\file");
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

    [Fact]
    public void HandlesFStringWithNoInterpolation()
    {
        var source = "f\"plain text\"";
        var tokens = Tokenize(source);
        // F-string with no interpolation should produce: FStringStart, FStringText, FStringEnd
        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringText && t.Value == "plain text");
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void HandlesFStringWithSimpleInterpolation()
    {
        var source = "f\"value is {x}\"";
        var tokens = Tokenize(source);
        // F-string with interpolation should produce: FStringStart, FStringText, FStringExprStart, Identifier, FStringExprEnd, FStringEnd
        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringText && t.Value == "value is ");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void HandlesFStringWithComplexExpression()
    {
        var source = "f\"result: {x + y * 2}\"";
        var tokens = Tokenize(source);
        // F-string with complex expression should produce: FStringStart, FStringText, FStringExprStart, expression tokens, FStringExprEnd, FStringEnd
        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringText && t.Value == "result: ");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Plus);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
        tokens.Should().Contain(t => t.Type == TokenType.Star);
        tokens.Should().Contain(t => t.Type == TokenType.Integer && t.Value == "2");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
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
    public void HandlesUnmatchedClosingBracket()
    {
        // Bug #2 fix: Unmatched closing bracket should not cause bracket depth to go negative
        var source = "x = ]";
        var tokens = Tokenize(source);

        // Should tokenize without error (parser will catch the error)
        tokens.Should().Contain(t => t.Type == TokenType.RightBracket);
        tokens.Should().Contain(t => t.Type == TokenType.Eof);
    }

    [Fact]
    public void HandlesMultipleUnmatchedClosingBrackets()
    {
        // Bug #2 fix: Multiple unmatched closing brackets
        var source = "x = ) ] }";
        var tokens = Tokenize(source);

        // Should tokenize without error
        tokens.Should().Contain(t => t.Type == TokenType.RightParen);
        tokens.Should().Contain(t => t.Type == TokenType.RightBracket);
        tokens.Should().Contain(t => t.Type == TokenType.RightBrace);
    }

    [Fact]
    public void HandlesBracketResetBetweenExpressions()
    {
        // Ensure bracket depth resets properly between expressions
        var source = @"x = [1]
y = [2]
z = [3]";
        var tokens = Tokenize(source);

        // Each line should have proper NEWLINE token
        tokens.Count(t => t.Type == TokenType.Newline).Should().Be(2);
        tokens.Count(t => t.Type == TokenType.LeftBracket).Should().Be(3);
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

    [Fact]
    public void HandlesIndentedCommentLine()
    {
        // Bug #1 fix: Indented comment lines should not produce extra NEWLINE tokens
        var source = @"if True:
    # indented comment
    x = 1";
        var tokens = Tokenize(source);

        // Should have: If, True, Colon, Newline, Indent, Identifier, Assign, Integer, Dedent, EOF
        // Should NOT have extra NEWLINE after the comment
        tokens.Should().Contain(t => t.Type == TokenType.Indent);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");

        // Count NEWLINE tokens - should be exactly 1 (after the colon)
        tokens.Count(t => t.Type == TokenType.Newline).Should().Be(1);
    }

    [Fact]
    public void HandlesCommentInsideBrackets()
    {
        var source = @"values = [
    # comment inside list
    1,
    # another comment
    2
]";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Integer && t.Value == "1");
        tokens.Should().Contain(t => t.Type == TokenType.Integer && t.Value == "2");
    }

    [Fact]
    public void HandlesMultipleBlankLinesWithVariousWhitespace()
    {
        // Bug #4 fix: Whitespace-only lines should not produce NEWLINE tokens
        var source = @"x = 1


y = 2";
        var tokens = Tokenize(source);

        // Should have: Identifier, Assign, Integer, Newline, Identifier, Assign, Integer, EOF
        // The blank lines (including the one with spaces) should be skipped
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");

        // Should have exactly 1 NEWLINE (after x = 1)
        tokens.Count(t => t.Type == TokenType.Newline).Should().Be(1);
    }

    [Fact]
    public void HandlesCommentAfterOpeningBracket()
    {
        var source = @"x = [  # comment after bracket
    1, 2, 3
]";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.LeftBracket);
        tokens.Should().Contain(t => t.Type == TokenType.Integer && t.Value == "1");
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

    [Fact]
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
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        tokens.Should().NotBeNull();
        // Error reported via diagnostics (if any) - both are acceptable
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
    public void ReportsErrorOnInvalidHexEscape()
    {
        var source = "\"\\xGG\"";  // Invalid hex digits
        var errorMessage = TokenizeExpectingError(source);
        errorMessage.Should().NotBeEmpty();
    }

    [Fact]
    public void ReportsErrorOnIncompleteHexEscape()
    {
        var source = "\"\\x4\"";  // Only one hex digit
        var errorMessage = TokenizeExpectingError(source);
        errorMessage.Should().NotBeEmpty();
    }

    [Fact]
    public void ReportsErrorOnIncompleteUnicodeEscape()
    {
        var source = "\"\\u004\"";  // Only 3 hex digits
        var errorMessage = TokenizeExpectingError(source);
        errorMessage.Should().NotBeEmpty();
    }

    [Fact]
    public void ReportsErrorOnInvalidUnicodeEscape()
    {
        var source = "\"\\u00GG\"";  // Invalid hex digits in Unicode
        var errorMessage = TokenizeExpectingError(source);
        errorMessage.Should().NotBeEmpty();
    }

    [Fact]
    public void ReportsErrorOnOctalValueTooLarge()
    {
        var source = "\"\\400\"";  // 256 in octal, exceeds max (255)
        var errorMessage = TokenizeExpectingError(source);
        errorMessage.Should().NotBeEmpty();
    }

    #region Astral \U Escapes (#879)

    private static LexerNs.Token FirstStringToken(string source)
        => Tokenize(source).First(t => t.Type == TokenType.String);

    [Fact]
    public void EightDigitUnicodeEscapeAboveBmpProducesSurrogatePair()
    {
        // \U0001F468 (MAN) is an astral code point; UTF-16 represents it as a
        // surrogate pair, so the decoded value must be two code units, not a
        // single truncated (char)0x1F468.
        var token = FirstStringToken("\"\\U0001F468\"");

        token.Value.Should().Be("\U0001F468");
        token.Value!.Length.Should().Be(2);
        char.IsHighSurrogate(token.Value[0]).Should().BeTrue();
        char.IsLowSurrogate(token.Value[1]).Should().BeTrue();
    }

    [Fact]
    public void EightDigitUnicodeEscapeForBmpCharDecodesToSingleCodeUnit()
    {
        // \U00000041 is the BMP code point 'A' and must stay a single code unit.
        var token = FirstStringToken("\"\\U00000041\"");

        token.Value.Should().Be("A");
        token.Value!.Length.Should().Be(1);
    }

    [Fact]
    public void MaxValidEightDigitUnicodeEscapeDecodesToSurrogatePair()
    {
        // U+10FFFF is the highest valid Unicode scalar value.
        var token = FirstStringToken("\"\\U0010FFFF\"");

        token.Value.Should().Be("\U0010FFFF");
        token.Value!.Length.Should().Be(2);
    }

    [Fact]
    public void EightDigitUnicodeEscapeOutOfRangeReportsError()
    {
        // U+110000 is past the Unicode ceiling; Python raises SyntaxError. The
        // lexer must diagnose this rather than silently truncating to 16 bits.
        var source = "\"\\U00110000\"";
        var errorMessage = TokenizeExpectingError(source);
        errorMessage.Should().Contain("out of range");
    }

    [Fact]
    public void LoneSurrogateUnicodeEscapeIsPreserved()
    {
        // A lone high surrogate (\uD83D) is <= 0xFFFF and stays a single code
        // unit — Python permits lone surrogates in str literals.
        var token = FirstStringToken("\"\\uD83D\"");

        token.Value!.Length.Should().Be(1);
        ((int)token.Value[0]).Should().Be(0xD83D);
        char.IsHighSurrogate(token.Value[0]).Should().BeTrue();
    }

    [Fact]
    public void EscapedAndLiteralAstralEmojiDecodeIdentically()
    {
        // The \U escape and the literal surrogate-pair glyph must decode to the
        // same two code units, proving the escape path is not lossy.
        var escaped = FirstStringToken("\"\\U0001F600\"");
        var literal = FirstStringToken("\"\U0001F600\"");

        escaped.Value.Should().Be(literal.Value);
        escaped.Value!.Length.Should().Be(2);
    }

    #endregion

    [Fact]
    public void ReportsErrorOnInvalidEscapeCharacter()
    {
        var source = "\"\\q\"";  // Invalid escape sequence
        var errorMessage = TokenizeExpectingError(source);
        errorMessage.Should().NotBeEmpty();
    }

    [Fact]
    public void ReportsErrorOnUnterminatedStringWithEscape()
    {
        var source = "\"hello\\n";  // Unterminated string
        var errorMessage = TokenizeExpectingError(source);
        errorMessage.Should().NotBeEmpty();
    }

    #endregion
}
