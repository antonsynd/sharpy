using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;
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
        // A literal opening with the f-string's own quote that never closes is the field's missing '}'
        // (python3.12: f"hello {x" → "f-string: expecting '}'"), not an unterminated literal.
        var errors = TokenizeExpectingError("f\"hello {x\"");
        errors.Should().Be("f-string: expecting '}'");
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
        // f"{x:{width}}" — a format spec is a mini f-string (PEP 701): the nested {width} is a
        // replacement field the lexer tokenizes, not raw text inside one FStringFormatSpec token.
        var tokens = Tokenize("f\"{x:{width}}\"");

        var i = 0;
        tokens[i++].Type.Should().Be(TokenType.FStringStart);
        tokens[i++].Type.Should().Be(TokenType.FStringExprStart);
        tokens[i].Type.Should().Be(TokenType.Identifier);
        tokens[i++].Value.Should().Be("x");
        tokens[i].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[i++].Value.Should().Be("");   // leading (empty) spec text — always emitted
        tokens[i++].Type.Should().Be(TokenType.FStringExprStart);  // nested {width}
        tokens[i].Type.Should().Be(TokenType.Identifier);
        tokens[i++].Value.Should().Be("width");
        tokens[i++].Type.Should().Be(TokenType.FStringExprEnd);    // close nested field
        tokens[i++].Type.Should().Be(TokenType.FStringExprEnd);    // close x field
        tokens[i].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_FormatSpecWithNestedExpressionAndType_TokenizesCorrectly()
    {
        // f"{x:{width}.{precision}f}" — literal spec text between/after the nested fields comes back
        // as its own FStringFormatSpec tokens ("", ".", "f").
        var tokens = Tokenize("f\"{x:{width}.{precision}f}\"");

        var i = 0;
        tokens[i++].Type.Should().Be(TokenType.FStringStart);
        tokens[i++].Type.Should().Be(TokenType.FStringExprStart);
        tokens[i].Type.Should().Be(TokenType.Identifier);
        tokens[i++].Value.Should().Be("x");
        tokens[i].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[i++].Value.Should().Be("");
        tokens[i++].Type.Should().Be(TokenType.FStringExprStart);  // {width}
        tokens[i].Type.Should().Be(TokenType.Identifier);
        tokens[i++].Value.Should().Be("width");
        tokens[i++].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[i].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[i++].Value.Should().Be(".");
        tokens[i++].Type.Should().Be(TokenType.FStringExprStart);  // {precision}
        tokens[i].Type.Should().Be(TokenType.Identifier);
        tokens[i++].Value.Should().Be("precision");
        tokens[i++].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[i].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[i++].Value.Should().Be("f");
        tokens[i++].Type.Should().Be(TokenType.FStringExprEnd);    // close x field
        tokens[i].Type.Should().Be(TokenType.FStringEnd);
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

    #region Conversion Flag Tests (#987)

    [Theory]
    [InlineData("f\"{x!r}\"", "r")]
    [InlineData("f\"{x!s}\"", "s")]
    [InlineData("f\"{x!a}\"", "a")]
    public void FString_ConversionFlag_EmitsFStringConversion(string source, string flag)
    {
        var tokens = Tokenize(source);

        tokens[0].Type.Should().Be(TokenType.FStringStart);
        tokens[1].Type.Should().Be(TokenType.FStringExprStart);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[2].Value.Should().Be("x");
        tokens[3].Type.Should().Be(TokenType.FStringConversion);
        tokens[3].Value.Should().Be(flag);
        tokens[4].Type.Should().Be(TokenType.FStringExprEnd);
        tokens[5].Type.Should().Be(TokenType.FStringEnd);
    }

    [Fact]
    public void FString_ConversionFlagFollowedByFormatSpec_EmitsBothTokens()
    {
        var tokens = Tokenize("f\"{x!r:>10}\"");

        tokens[1].Type.Should().Be(TokenType.FStringExprStart);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[3].Type.Should().Be(TokenType.FStringConversion);
        tokens[3].Value.Should().Be("r");
        tokens[4].Type.Should().Be(TokenType.FStringFormatSpec);
        tokens[4].Value.Should().Be(">10");
        tokens[5].Type.Should().Be(TokenType.FStringExprEnd);
    }

    [Fact]
    public void FString_NotEqualOperator_NotTreatedAsConversion()
    {
        // The '!=' inside a replacement field must tokenize as the NotEqual operator,
        // never as a conversion flag.
        var tokens = Tokenize("f\"{a != b}\"");

        tokens.Should().Contain(t => t.Type == TokenType.NotEqual);
        tokens.Should().NotContain(t => t.Type == TokenType.FStringConversion);
    }

    [Fact]
    public void FString_InvalidConversionFlag_ReportsError()
    {
        var errors = TokenizeExpectingError("f\"{x!q}\"");
        errors.Should().Contain("Invalid f-string conversion");
    }

    #endregion

    #region PEP 701 hole grammar (#2022)

    [Theory]
    [InlineData("f\"{x\n}\"")]                  // newline before '}' in a single-quoted f-string
    [InlineData("f\"{\nx}\"")]                  // newline after '{'
    [InlineData("f\"{x +\n1}\"")]               // inside a binary
    [InlineData("f\"{x\r\n+ 1}\"")]             // CRLF
    [InlineData("f\"{x\r+ 1}\"")]               // bare CR
    [InlineData("f\"{x # c\n}\"")]              // comment to end of line
    [InlineData("f\"{x # }\n}\"")]              // '}' inside a comment closes nothing
    [InlineData("f\"{x \\\n+ 1}\"")]            // backslash continuation
    [InlineData("f\"{x!r\n}\"")]                // newline after a conversion
    [InlineData("f\"{x!r # c\n:>4}\"")]         // comment after a conversion, then a spec
    [InlineData("f\"{\fx}\"")]                  // form feed
    [InlineData("t\"{x # c\n=}\"")]
    public void Hole_WhitespaceNewlinesAndComments_Lex(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeFalse(string.Join("; ", lexer.Diagnostics.GetErrors().Select(d => d.Code + " " + d.Message)));
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
        tokens.Should().NotContain(t => t.Type == TokenType.Newline);
    }

    [Theory]
    [InlineData("print(f\"a {x)\ny = 1\n", ')', 13)]
    [InlineData("print(f\"a {x]\ny = 1\n", ']', 13)]
    [InlineData("print(f\"a {(x))}\"\ny = 1\n", ')', 15)]
    public void Hole_UnmatchedCloser_IsRefusedWhereItStands(string source, char closer, int column)
    {
        // CPython: "f-string: unmatched ')'". Without the refusal a hole that may now span lines would
        // swallow the following lines (the unclosed hole reports far from its cause).
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var error = lexer.Diagnostics.GetErrors().Should().ContainSingle().Subject;
        error.Code.Should().Be(Sharpy.Compiler.Diagnostics.DiagnosticCodes.Lexer.UnmatchedBraceInFString);
        error.Message.Should().Be($"f-string: unmatched '{closer}'");
        error.Line.Should().Be(1);
        error.Column.Should().Be(column);
        // Recovery resumes on the next line: 'y = 1' lexes as its own statement.
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y" && t.Line == 2);
    }

    /// <summary>
    /// Every hole diagnostic spells the string kind (CPython 3.14: <c>t"{x)}"</c> →
    /// <c>t-string: unmatched ')'</c>): (source with <c>P</c> for the prefix, message with <c>K</c> for the
    /// kind). A df-string is an f-string.
    /// </summary>
    public static TheoryData<string, string, string, string> KindSpelledCells()
    {
        var cells = new (string Source, string Message)[]
        {
            ("P\"hello", "Unterminated K"),
            ("P\"hello }\"", "Unmatched '}' in K"),
            ("P\"{x!q}\"", "Invalid K conversion '!q'. Expected '!r', '!s', or '!a' followed by '}' or ':'."),
            ("P\"{x!r\"", "Unterminated K expression: expected '}' or ':' after the conversion flag"),
            ("P\"{x)}\"", "K: unmatched ')'"),
            ("P\"{x]}\"", "K: unmatched ']'"),
        };
        var data = new TheoryData<string, string, string, string>();
        foreach (var (prefix, kind) in new[] { ("f", "f-string"), ("t", "t-string"), ("df", "f-string") })
        {
            foreach (var (source, message) in cells)
                data.Add(prefix, kind, source, message);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(KindSpelledCells))]
    public void HoleDiagnostic_SpellsTheStringKind(string prefix, string kind, string source, string message)
    {
        var lexer = new LexerNs.Lexer("v = " + source.Replace("P", prefix, StringComparison.Ordinal) + "\n");
        lexer.TokenizeAll();
        var error = lexer.Diagnostics.GetErrors().Should().ContainSingle().Subject;
        error.Message.Should().Be(message.Replace("K", kind, StringComparison.Ordinal));
    }

    /// <summary>
    /// An unclosed replacement field is reported where python reports it — never at the end of the
    /// file (P22 Decision 13: an unclosed hole cannot swallow the following lines). Cells: (label, hole
    /// source after the opening quote, the character marking the expected column on line 2 — the first
    /// one after the opening quote, or <c>Q</c> for the second quote — message, code). Axes: prefix {f,
    /// t, df} × quoting {single, triple}. python3.12/3.14: <c>f"{x</c> → <c>'{' was never closed</c> at the
    /// <c>{</c>; <c>f"{x"</c> → <c>f-string: expecting '}'</c> at the second quote; <c>f"{(x</c> →
    /// <c>'(' was never closed</c> at the <c>(</c>; <c>f"{x:{w</c> → the inner <c>{</c>; <c>f"{x:>4"</c> →
    /// <c>f-string: expecting '}', or format specs</c> at the second quote.
    /// </summary>
    public static TheoryData<string, string, string, string, string, char, string, string> UnclosedFieldCells()
    {
        const string swallowedTail = "\n\ndef g(y: int) -> int:\n    return y\n";
        var cells = new (string Label, string Hole, string Tail, char Marker, string Message, string Code)[]
        {
            ("comment_swallows_close", "{x # the value}Q", swallowedTail, '{', "'{' was never closed", DiagnosticCodes.Lexer.UnterminatedFormatSpec),
            ("comment_swallows_close_no_spec", "{x # the value}Q", "\ny = 1\n", '{', "'{' was never closed", DiagnosticCodes.Lexer.UnterminatedFStringExpression),
            ("missing_close_at_eol", "{xQ", "\n", 'Q', "K: expecting '}'", DiagnosticCodes.Lexer.UnterminatedFStringExpression),
            ("missing_close_at_eof", "{x", "", '{', "'{' was never closed", DiagnosticCodes.Lexer.UnterminatedFStringExpression),
            ("paren_open_at_eof", "{(x + 1", "", '(', "'(' was never closed", DiagnosticCodes.Lexer.UnterminatedFStringExpression),
            ("nested_spec_field_at_eof", "{x:{w", "", '}', "'{' was never closed", DiagnosticCodes.Lexer.UnterminatedFStringExpression),
            ("conversion_at_eof", "{x!r", "", '{', "'{' was never closed", DiagnosticCodes.Lexer.UnterminatedFStringExpression),
            ("spec_at_eof", "{x:>4", "", '{', "'{' was never closed", DiagnosticCodes.Lexer.UnterminatedFormatSpec),
            ("spec_ended_by_quote", "{x:>4Q", "\nz = {1: 2}\n", 'Q', "K: expecting '}', or format specs", DiagnosticCodes.Lexer.UnterminatedFormatSpec),
            // The quote that ends the string sits lines after the swallowing comment: the field is
            // reported at its '{', not at that later quote.
            ("comment_swallows_close_spec_ended_by_later_quote", "{x # the value}Q", "\n\ndef g(y: int) -> str:\n    return QhiQ\n", '{', "'{' was never closed", DiagnosticCodes.Lexer.UnterminatedFormatSpec),
            ("comment_swallows_close_unterminated_later_literal", "{x # the value}Q", "\ns = Qabc\n", '{', "'{' was never closed", DiagnosticCodes.Lexer.UnterminatedFStringExpression),
        };
        var data = new TheoryData<string, string, string, string, string, char, string, string>();
        foreach (var (prefix, kind) in new[] { ("f", "f-string"), ("t", "t-string"), ("df", "f-string") })
        {
            foreach (var quote in new[] { "\"", "\"\"\"" })
            {
                foreach (var (label, hole, tail, marker, message, code) in cells)
                    data.Add(label, prefix, quote, hole, tail, marker, message.Replace("K", kind, StringComparison.Ordinal), code);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(UnclosedFieldCells))]
    public void UnclosedField_IsReportedAtItsBracket_NotAtEof(string label, string prefix, string quote, string hole, string tail, char marker, string message, string code)
    {
        var line2 = "    return " + prefix + quote + hole.Replace("Q", quote, StringComparison.Ordinal);
        var source = "def f(x: int) -> str:\n" + line2 + tail.Replace("Q", quote, StringComparison.Ordinal);
        var afterQuote = 11 + prefix.Length + quote.Length;
        // The expected column: the marker character on line 2 (the '}' marker means the second '{').
        var column = marker switch
        {
            'Q' => line2.IndexOf(quote, afterQuote, StringComparison.Ordinal),
            '}' => line2.IndexOf('{', line2.IndexOf('{', afterQuote) + 1),
            _ => line2.IndexOf(marker, afterQuote),
        } + 1;

        var lexer = new LexerNs.Lexer(source);
        lexer.TokenizeAll();
        var errors = lexer.Diagnostics.GetErrors().ToList();
        // A single-quoted string resumes on the line after the bracket, where the unterminated later
        // literal is a genuine second error; every other tail is clean code.
        var expectedCount = label.EndsWith("_later_literal", StringComparison.Ordinal) && quote.Length == 1 ? 2 : 1;
        errors.Should().HaveCount(expectedCount, label + ": " + source);
        var error = errors[0];
        error.Message.Should().Be(message, label);
        error.Code.Should().Be(code, label);
        error.Line.Should().Be(2, label);
        error.Column.Should().Be(column, label);
    }

    [Theory]
    [InlineData("f")]
    [InlineData("t")]
    [InlineData("df")]
    public void UnclosedField_InASingleQuotedString_ResumesOnTheNextLine(string prefix)
    {
        // The single-quoted string's text cannot span lines, so what the swallowing hole lexed past its
        // '{' is dropped and lexing resumes after that line: a later lexer error still surfaces.
        var lexer = new LexerNs.Lexer("def f(x: int) -> str:\n    return " + prefix + "\"{x # the value}\"\n\ndef g() -> None:\n    y = 1 $ 2\n");
        var tokens = lexer.TokenizeAll();
        lexer.Diagnostics.GetErrors().Select(d => (d.Message, d.Line))
            .Should().Equal(("'{' was never closed", 2), ("Unexpected character: '$'", 5));
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "g" && t.Line == 4);
        tokens.Should().NotContain(t => t.Line > 2 && t.Line < 4, "nothing the swallowing hole lexed survives");
        tokens.Select(t => t.Position).Should().BeInAscendingOrder("the dropped tokens leave the stream monotonic");
    }

    [Fact]
    public void UnclosedField_InATripleQuotedString_DoesNotResume()
    {
        // A triple-quoted string's text spans lines: nothing after the '{' is known to be code, so the
        // one diagnostic at the '{' is the whole report (no cascade from re-lexing string text as code).
        var lexer = new LexerNs.Lexer("def f(x: int) -> str:\n    return f\"\"\"{x # c}\"\"\"\n\ndef g() -> None:\n    y = 1 $ 2\n");
        lexer.TokenizeAll();
        var error = lexer.Diagnostics.GetErrors().Should().ContainSingle().Subject;
        (error.Message, error.Line, error.Column).Should().Be(("'{' was never closed", 2, 16));
    }

    [Theory]
    [InlineData("`x`")]
    [InlineData("Color.`red`")]
    [InlineData("`x` + `y`")]
    [InlineData("`System.Int32`")]
    public void Hole_BacktickName_LexesAsTheMainLoop(string expression)
    {
        // One hole grammar (P22): the hole tokenizer's name arm IS the main loop's, so a backtick-escaped
        // name inside a hole is the same token sequence as at statement level (it was SPY0015).
        static string Describe(IEnumerable<LexerNs.Token> tokens) =>
            string.Join(" ", tokens.Select(t => $"{t.Type}:{t.Value}:{t.IsBacktickEscaped}:{t.Length}"));

        var topLexer = new LexerNs.Lexer("v = " + expression + "\n");
        var top = topLexer.TokenizeAll().Skip(2).TakeWhile(t => t.Type is not (TokenType.Newline or TokenType.Eof));
        foreach (var prefix in new[] { "f\"", "t\"", "df\"", "f\"\"\"" })
        {
            var close = prefix.Length == 4 ? "\"\"\"" : "\"";
            var lexer = new LexerNs.Lexer("v = " + prefix + "{" + expression + "}" + close + "\n");
            var tokens = lexer.TokenizeAll();
            lexer.Diagnostics.HasErrors.Should().BeFalse(prefix + ": " + string.Join("; ", lexer.Diagnostics.GetErrors().Select(d => d.Code + " " + d.Message)));
            var inHole = tokens.SkipWhile(t => t.Type != TokenType.FStringExprStart).Skip(1).TakeWhile(t => t.Type != TokenType.FStringExprEnd);
            Describe(inHole).Should().Be(Describe(top), prefix);
        }
    }

    [Theory]
    [InlineData("{`a{b`}")]
    [InlineData("{`a#b`}")]
    [InlineData("{`a'b`}")]
    public void DedentPrescan_TreatsABacktickNameAsOpaque(string hole)
    {
        // The df dedent prescan follows the hole grammar: a '{', '#' or quote inside a backtick-escaped
        // name opens, comments or quotes nothing, so the closing line's indent is still found.
        var tokens = Tokenize("v = df\"\"\"\n    a" + hole + "b\n    \"\"\"\n");
        tokens.Where(t => t.Type == TokenType.FStringText).Select(t => t.Value).Should().Equal("a", "b");
    }

    [Fact]
    public void Hole_Comment_IsCommentTrivia_WhenTriviaIsPreserved()
    {
        var lexer = new LexerNs.Lexer("v = f\"{x # note\n}\"\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();
        var trivia = tokens.SelectMany(t => (t.LeadingTrivia ?? Array.Empty<LexerNs.Trivia>()).Concat(t.TrailingTrivia ?? Array.Empty<LexerNs.Trivia>()));
        trivia.Should().ContainSingle(t => t.Kind == LexerNs.TriviaKind.Comment && t.Text == "# note" && t.Line == 1 && t.Column == 10);
    }

    #endregion
}
