#pragma warning disable CS0618 // LexerError is obsolete

using FluentAssertions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;
using LexerError = Sharpy.Compiler.Lexer.LexerError;
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// Comprehensive tests for f-string segmented lexing
/// </summary>
public class FStringLexerTests
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

    #region Positive Tests

    [Fact]
    public void FString_Empty_EmitsStartAndEnd()
    {
        var tokens = Tokenize("f\"\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[0].Value.Should().Be("f\"");
        tokens[1].Type.Should().Be(TokenType.FStringEnd);
        tokens[1].Value.Should().Be("\"");
        tokens[2].Type.Should().Be(TokenType.Eof);
    }

    [Fact]
    public void FString_TextOnly_EmitsStartTextEnd()
    {
        var tokens = Tokenize("f\"hello world\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringText);
        tokens[1].Value.Should().Be("hello world");
        tokens[2].Type.Should().Be(TokenType.FStringEnd);
        tokens[3].Type.Should().Be(TokenType.Eof);
    }

    [Fact]
    public void FString_SingleExpression_EmitsCorrectSequence()
    {
        var tokens = Tokenize("f\"{x}\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringExprStart);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[2].Value.Should().Be("x");
        tokens[3].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[4].Type.Should().Be(TokenType.FStringEnd);
        tokens[5].Type.Should().Be(TokenType.Eof);
    }

    [Fact]
    public void FString_TextBeforeExpression_EmitsCorrectSequence()
    {
        var tokens = Tokenize("f\"value: {x}\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringText);
        tokens[1].Value.Should().Be("value: ");
        tokens[2].Type.Should().Be(TokenType.FStringExprStart);
        tokens[3].Type.Should().Be(TokenType.Identifier);
        tokens[3].Value.Should().Be("x");
        tokens[4].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[5].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_TextAfterExpression_EmitsCorrectSequence()
    {
        var tokens = Tokenize("f\"{x} units\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringExprStart);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[2].Value.Should().Be("x");
        tokens[3].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[4].Type.Should().Be(TokenType.FStringText);
        tokens[4].Value.Should().Be(" units");
        tokens[5].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_MultipleExpressions_EmitsCorrectSequence()
    {
        var tokens = Tokenize("f\"{x} + {y} = {z}\"");

        var i = 0;
        tokens[i++].Type.Should().Be(TokenType.FStringStart);
        tokens[i++].Type.Should().Be(TokenType.FStringExprStart);
        tokens[i++].Type.Should().Be(TokenType.Identifier); // x
        tokens[i++].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[i++].Type.Should().Be(TokenType.FStringText); // " + "
        tokens[i++].Type.Should().Be(TokenType.FStringExprStart);
        tokens[i++].Type.Should().Be(TokenType.Identifier); // y
        tokens[i++].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[i++].Type.Should().Be(TokenType.FStringText); // " = "
        tokens[i++].Type.Should().Be(TokenType.FStringExprStart);
        tokens[i++].Type.Should().Be(TokenType.Identifier); // z
        tokens[i++].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[i++].Type.Should().Be(TokenType.FStringEnd);
        tokens[i++].Type.Should().Be(TokenType.Eof);
    }

    [Fact]
    public void FString_ComplexExpression_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"result: {x + y * 2}\"");

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
    public void FString_MethodCall_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"name: {obj.getName()}\"");

        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "obj");
        tokens.Should().Contain(t => t.Type == TokenType.Dot);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "getName");
        tokens.Should().Contain(t => t.Type == TokenType.LeftParen);
        tokens.Should().Contain(t => t.Type == TokenType.RightParen);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void FString_IndexAccess_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"item: {items[0]}\"");

        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "items");
        tokens.Should().Contain(t => t.Type == TokenType.LeftBracket);
        tokens.Should().Contain(t => t.Type == TokenType.Integer && t.Value == "0");
        tokens.Should().Contain(t => t.Type == TokenType.RightBracket);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void FString_DictLiteralInExpression_TokenizesCorrectly()
    {
        // {y} inside the expression is a set literal, not an f-string interpolation
        var tokens = Tokenize("f\"result: {calc({y})}\"");

        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "calc");
        tokens.Should().Contain(t => t.Type == TokenType.LeftParen);
        tokens.Should().Contain(t => t.Type == TokenType.LeftBrace); // set literal
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
        tokens.Should().Contain(t => t.Type == TokenType.RightBrace); // set literal
        tokens.Should().Contain(t => t.Type == TokenType.RightParen);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void FString_NestedParens_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{func(a, (b, c))}\"");

        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "func");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void FString_EscapedBraces_TokenizesAsText()
    {
        var tokens = Tokenize("f\"{{escaped}}\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringText);
        tokens[1].Value.Should().Be("{escaped}"); // {{ and }} become { and }
        tokens[2].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_EscapeSequences_ProcessedCorrectly()
    {
        var tokens = Tokenize("f\"line1\\nline2\"");

        tokens[1].Type.Should().Be(TokenType.FStringText);
        tokens[1].Value.Should().Contain("\n");
    }

    [Fact]
    public void FString_SingleQuoted_TokenizesCorrectly()
    {
        var tokens = Tokenize("f'hello {name}'");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[0].Value.Should().Be("f'");
        tokens[1].Type.Should().Be(TokenType.FStringText);
        tokens[2].Type.Should().Be(TokenType.FStringExprStart);
        tokens[3].Type.Should().Be(TokenType.Identifier);
        tokens[4].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[5].Type.Should().Be(TokenType.FStringEnd);
        tokens[5].Value.Should().Be("'");
    }

    [Fact]
    public void FString_TripleQuoted_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"\"\"hello {name}\"\"\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[0].Value.Should().Be("f\"\"\"");
        tokens[1].Type.Should().Be(TokenType.FStringText);
        tokens[2].Type.Should().Be(TokenType.FStringExprStart);
        tokens[3].Type.Should().Be(TokenType.Identifier);
        tokens[4].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[5].Type.Should().Be(TokenType.FStringEnd);
        tokens[5].Value.Should().Be("\"\"\"");
    }

    [Fact]
    public void FString_TripleQuotedWithNewlines_TokenizesCorrectly()
    {
        var source = @"f""""""line1
{x}
line2""""""";
        var tokens = Tokenize(source);

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringText);
        tokens[1].Value.Should().Contain("line1\n");
        tokens[2].Type.Should().Be(TokenType.FStringExprStart);
        tokens[3].Type.Should().Be(TokenType.Identifier);
        tokens[4].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[5].Type.Should().Be(TokenType.FStringText);
        tokens[5].Value.Should().Contain("\nline2");
        tokens[6].Type.Should().Be(TokenType.FStringEnd);
    }

    #endregion

    #region Negative Tests

    [Fact]
    public void FString_Unterminated_ThrowsError()
    {
        var errors = TokenizeExpectingError("f\"hello");
        errors.Should().Contain("Unterminated f-string");
    }

    [Fact]
    public void FString_UnterminatedExpression_ThrowsError()
    {
        // When the expression has an unterminated string inside it, we get "Unterminated string literal"
        var errors = TokenizeExpectingError("f\"hello {x\"");
        errors.Should().Contain("Unterminated string");
    }

    [Fact]
    public void FString_UnmatchedClosingBrace_ThrowsError()
    {
        var errors = TokenizeExpectingError("f\"hello }\"");
        errors.Should().Contain("Unmatched '}'");
    }

    [Fact]
    public void FString_SingleQuoteUnterminated_ThrowsError()
    {
        var errors = TokenizeExpectingError("f'hello");
        errors.Should().Contain("Unterminated f-string");
    }

    [Fact]
    public void FString_TripleQuotedUnterminated_ThrowsError()
    {
        var errors = TokenizeExpectingError("f\"\"\"hello");
        errors.Should().Contain("Unterminated");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void FString_ConsecutiveExpressions_NoTextBetween()
    {
        var tokens = Tokenize("f\"{x}{y}\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringExprStart);
        tokens[2].Type.Should().Be(TokenType.Identifier); // x
        tokens[3].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[4].Type.Should().Be(TokenType.FStringExprStart); // immediately followed by next expression
        tokens[5].Type.Should().Be(TokenType.Identifier); // y
        tokens[6].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[7].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_ExpressionWithWhitespace_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{ x + y }\"");

        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Plus);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
    }

    [Fact]
    public void FString_StringLiteralInExpression_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{func('test')}\"");

        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "func");
        tokens.Should().Contain(t => t.Type == TokenType.String && t.Value == "test");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void FString_EmptyExpression_TokenizesCorrectly()
    {
        // This is technically invalid Python but let's see what our lexer does
        var tokens = Tokenize("f\"{}\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringExprStart);
        tokens[2].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[3].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_OnlyEscapedBraces_TokenizesAsText()
    {
        var tokens = Tokenize("f\"{{}}\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringText);
        tokens[1].Value.Should().Be("{}");
        tokens[2].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_MixedTextAndExpressions_ComplexCase()
    {
        var tokens = Tokenize("f\"prefix {a} middle {b + c} suffix\"");

        var i = 0;
        tokens[i++].Type.Should().Be(TokenType.FStringStart);
        tokens[i].Type.Should().Be(TokenType.FStringText);
        tokens[i].Value.Should().Be("prefix ");
        i++;
        tokens[i++].Type.Should().Be(TokenType.FStringExprStart);
        tokens[i++].Type.Should().Be(TokenType.Identifier); // a
        tokens[i++].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[i].Type.Should().Be(TokenType.FStringText);
        tokens[i].Value.Should().Be(" middle ");
        i++;
        tokens[i++].Type.Should().Be(TokenType.FStringExprStart);
        tokens[i++].Type.Should().Be(TokenType.Identifier); // b
        tokens[i++].Type.Should().Be(TokenType.Plus);
        tokens[i++].Type.Should().Be(TokenType.Identifier); // c
        tokens[i++].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[i].Type.Should().Be(TokenType.FStringText);
        tokens[i].Value.Should().Be(" suffix");
        i++;
        tokens[i++].Type.Should().Be(TokenType.FStringEnd);
    }

    #endregion

    #region Format Specifier Tests

    [Fact]
    public void FString_SimpleFormatSpec_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{x:.2f}\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringExprStart);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[2].Value.Should().Be("x");
        tokens[3].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[3].Value.Should().Be(".2f");
        tokens[4].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[5].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_FormatSpecAlignment_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{x:>10}\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringExprStart);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[2].Value.Should().Be("x");
        tokens[3].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[3].Value.Should().Be(">10");
        tokens[4].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[5].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_FormatSpecComplex_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{x:0>5.2f}\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringExprStart);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[2].Value.Should().Be("x");
        tokens[3].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[3].Value.Should().Be("0>5.2f");
        tokens[4].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[5].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_EmptyFormatSpec_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{x:}\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringExprStart);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[2].Value.Should().Be("x");
        tokens[3].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[3].Value.Should().Be("");  // Empty format spec
        tokens[4].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[5].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_FormatSpecWithNestedExpression_TokenizesCorrectly()
    {
        // f"{x:{width}}" - format spec contains nested expression
        var tokens = Tokenize("f\"{x:{width}}\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringExprStart);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[2].Value.Should().Be("x");
        tokens[3].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[3].Value.Should().Be("{width}");  // Nested braces included in format spec
        tokens[4].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[5].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_FormatSpecWithNestedExpressionAndType_TokenizesCorrectly()
    {
        // f"{x:{width}.{precision}f}" - format spec with multiple nested expressions
        var tokens = Tokenize("f\"{x:{width}.{precision}f}\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringExprStart);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[2].Value.Should().Be("x");
        tokens[3].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[3].Value.Should().Be("{width}.{precision}f");
        tokens[4].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[5].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_MultipleExpressionsWithFormatSpecs_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{x:.2f} and {y:>10}\"");

        var i = 0;
        tokens[i++].Type.Should().Be(TokenType.FStringStart);
        tokens[i++].Type.Should().Be(TokenType.FStringExprStart);
        tokens[i++].Type.Should().Be(TokenType.Identifier); // x
        tokens[i].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[i++].Value.Should().Be(".2f");
        tokens[i++].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[i].Type.Should().Be(TokenType.FStringText);
        tokens[i++].Value.Should().Be(" and ");
        tokens[i++].Type.Should().Be(TokenType.FStringExprStart);
        tokens[i++].Type.Should().Be(TokenType.Identifier); // y
        tokens[i].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[i++].Value.Should().Be(">10");
        tokens[i++].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[i].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_ColonInNestedExpression_NotTreatedAsFormatSpec()
    {
        // Colon at depth > 1 (inside dict literal) should not be treated as format spec
        var tokens = Tokenize("f\"{calc({'a': 1})}\"");

        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "calc");
        tokens.Should().Contain(t => t.Type == TokenType.LeftParen);
        tokens.Should().Contain(t => t.Type == TokenType.LeftBrace);  // Dict literal start
        tokens.Should().Contain(t => t.Type == TokenType.String && t.Value == "a");
        tokens.Should().Contain(t => t.Type == TokenType.Colon);  // Colon inside dict literal at depth > 1
        tokens.Should().Contain(t => t.Type == TokenType.Integer && t.Value == "1");
        tokens.Should().Contain(t => t.Type == TokenType.RightBrace);  // Dict literal end
        tokens.Should().Contain(t => t.Type == TokenType.RightParen);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
        // Should not have FStringFormatSpec token (colon was inside nested dict, not at depth 1)
        tokens.Should().NotContain(t => t.Type == TokenType.FStringFormatSpec);
    }

    [Fact]
    public void FString_TernaryOperator_TokenizesCorrectly()
    {
        // Test that f-strings work with ternary expressions (x if condition else y)
        // This does not involve colon handling; ternary uses 'if' and 'else' keywords.
        var tokens = Tokenize("f\"{x if condition else y}\"");

        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.If);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "condition");
        tokens.Should().Contain(t => t.Type == TokenType.Else);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
    }

    [Fact]
    public void FString_FormatSpecWithSpaces_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{x: >10}\"");

        tokens[3].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[3].Value.Should().Be(" >10");  // Space is part of format spec
    }

    [Fact]
    public void FString_FormatSpecPercentage_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{x:.2%}\"");

        tokens[3].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[3].Value.Should().Be(".2%");
    }

    [Fact]
    public void FString_FormatSpecBinary_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{x:08b}\"");

        tokens[3].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[3].Value.Should().Be("08b");
    }

    [Fact]
    public void FString_FormatSpecHex_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{x:#06x}\"");

        tokens[3].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[3].Value.Should().Be("#06x");
    }

    [Fact]
    public void FString_ComplexExpressionWithFormatSpec_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{x + y:.2f}\"");

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringExprStart);
        tokens[2].Type.Should().Be(TokenType.Identifier); // x
        tokens[3].Type.Should().Be(TokenType.Plus);
        tokens[4].Type.Should().Be(TokenType.Identifier); // y
        tokens[5].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[5].Value.Should().Be(".2f");
        tokens[6].Type.Should().Be(TokenType.FStringExprEnd);
    }

    [Fact]
    public void FString_MethodCallWithFormatSpec_TokenizesCorrectly()
    {
        var tokens = Tokenize("f\"{obj.getValue():.2f}\"");

        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "obj");
        tokens.Should().Contain(t => t.Type == TokenType.Dot);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "getValue");
        tokens.Should().Contain(t => t.Type == TokenType.LeftParen);
        tokens.Should().Contain(t => t.Type == TokenType.RightParen);
        var formatSpecToken = tokens.First(t => t.Type == TokenType.FStringFormatSpec);
        formatSpecToken.Value.Should().Be(".2f");
    }

    #endregion
}
