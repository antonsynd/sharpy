using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;
using TokenType = Sharpy.Compiler.Lexer.TokenType;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// A t-string hole's <c>Interpolation.expression</c> is its SOURCE text (#1991): from just after
/// <c>{</c> to the top-level terminator (<c>}</c>, <c>=</c>, <c>!</c>, <c>:</c>), leading whitespace
/// kept, trailing whitespace stripped. The lexer captures it on the terminator token and the parser
/// copies it into <see cref="FStringPart.ExpressionText"/>. Every expected text is python3.14's, e.g.
/// <code>/opt/homebrew/bin/python3.14 -c 'x=1; print(repr(t"{ x !r}".interpolations[0].expression))'</code>
/// → <c>' x'</c>. The executed twin (the emitted <c>Interpolation</c> through <c>repr()</c>) is
/// <c>TemplateSurfaceMatrixTests</c>.
/// </summary>
public class TStringExpressionTextTests
{
    private static List<LexerNs.Token> Tokenize(string source) => new LexerNs.Lexer(source).TokenizeAll();

    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeFalse("the cell must lex: " + source);
        return new ParserNs.Parser(tokens).ParseModule();
    }

    private static FStringPart FirstHole(string tstring)
    {
        var module = Parse("v = " + tstring + "\n");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var literal = assignment.Value.Should().BeOfType<TStringLiteral>().Subject;
        return literal.Parts.First(p => p.Expression != null);
    }

    /// <summary>(t-string source, python3.14 expression text, the terminator token that carries it).</summary>
    public static TheoryData<string, string, TokenType> Cells => new()
    {
        { "t\"{x}\"", "x", TokenType.FStringExprEnd },
        { "t\"{ x}\"", " x", TokenType.FStringExprEnd },                 // leading ws kept
        { "t\"{x }\"", "x", TokenType.FStringExprEnd },                  // trailing ws stripped
        { "t\"{ x }\"", " x", TokenType.FStringExprEnd },                // both
        { "t\"{\tx\t}\"", "\tx", TokenType.FStringExprEnd },             // tabs
        { "t\"{x=}\"", "x", TokenType.FStringSelfDoc },
        { "t\"{ x = }\"", " x", TokenType.FStringSelfDoc },
        { "t\"{x = }\"", "x", TokenType.FStringSelfDoc },
        { "t\"{x  =  :>5}\"", "x", TokenType.FStringSelfDoc },
        { "t\"{ x !r}\"", " x", TokenType.FStringConversion },           // '!' after ws
        { "t\"{ x :>5}\"", " x", TokenType.FStringFormatSpec },          // ':' after ws
        { "t\"{x:{w}}\"", "x", TokenType.FStringFormatSpec },            // nested spec
        { "t\"{ (x + 1) }\"", " (x + 1)", TokenType.FStringExprEnd },     // parenthesised
        { "t\"{xs.pop(0)}\"", "xs.pop(0)", TokenType.FStringExprEnd },   // call with args
        { "t\"{d['k']}\"", "d['k']", TokenType.FStringExprEnd },         // index, quote choice kept
        { "t\"{'{'}\"", "'{'", TokenType.FStringExprEnd },               // brace inside a string literal
        { "t\"{ {'a':1}['a'] }\"", " {'a':1}['a']", TokenType.FStringExprEnd }, // dict braces inside the hole
        { "t\"{x != 1}\"", "x != 1", TokenType.FStringExprEnd },         // '!=' is not a terminator
        { "t\"{f(a=1)}\"", "f(a=1)", TokenType.FStringExprEnd },         // '=' inside () is not a terminator
    };

    [Theory]
    [MemberData(nameof(Cells))]
    public void Parser_CopiesTheHoleSourceText(string tstring, string expected, TokenType terminator)
    {
        _ = terminator;
        FirstHole(tstring).ExpressionText.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Lexer_AttachesTheTextToTheFirstTerminator(string tstring, string expected, TokenType terminator)
    {
        var tokens = Tokenize(tstring);
        var first = tokens.First(t => t.FStringExpressionText != null);
        first.Type.Should().Be(terminator);
        first.FStringExpressionText.Should().Be(expected);
    }

    [Fact]
    public void Lexer_TerminatorSpansAreUnchanged()
    {
        // The text rides beside the token's Value, never in it: '}' stays one character wide and
        // '!r' two, so LSP semantic-token spans stay monotonic (FrontEndParity).
        var close = Tokenize("t\"{ x }\"").Single(t => t.Type == TokenType.FStringExprEnd);
        close.Value.Should().Be("}");
        close.Length.Should().Be(1);

        var conversion = Tokenize("t\"{ x !r}\"").Single(t => t.Type == TokenType.FStringConversion);
        conversion.Value.Should().Be("r");
        conversion.Length.Should().Be(1);
    }

    [Fact]
    public void Parser_NestedSpecFieldCarriesItsOwnText()
    {
        var hole = FirstHole("t\"{x:{ w }}\"");
        hole.ExpressionText.Should().Be("x");
        // Not observable in python (the spec is evaluated to text); the same lexer rule applies.
        var nested = hole.Spec!.Value.First(p => p.Expression != null);
        nested.ExpressionText.Should().Be(" w");
    }

    [Fact]
    public void Parser_NestedFStringInsideAHoleKeepsTheOuterText()
    {
        FirstHole("t\"{ f'{x}' }\"").ExpressionText.Should().Be(" f'{x}'");
    }

    [Fact]
    public void Parser_SecondHoleHasItsOwnText()
    {
        var module = Parse("v = t\"a{ x }b{y!r}\"\n");
        var literal = ((Assignment)module.Body[0]).Value.Should().BeOfType<TStringLiteral>().Subject;
        literal.Parts.Where(p => p.Expression != null).Select(p => p.ExpressionText)
            .Should().Equal(" x", "y");
    }
}
