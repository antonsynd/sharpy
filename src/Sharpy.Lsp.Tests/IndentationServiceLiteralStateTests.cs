using FluentAssertions;
using Sharpy.Compiler.Lexer;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// <see cref="IndentationService.BuildIndentMap"/> surfaces the lexer's
/// <c>LiteralStateUnknown</c> (P22e decision 7, #2168) next to the map it computes.
/// </summary>
public class IndentationServiceLiteralStateTests
{
    /// <summary>D19: a docstring opened ABOVE an existing multi-line string.</summary>
    private const string D19 =
        "def f() -> int:\n    \"\"\"\n    return 1\ndef g() -> str:\n    s = \"\"\"\n        key: value\n    \"\"\"\n    return s\n";

    /// <summary>D19 without the inserted opener: lexes clean.</summary>
    private const string Clean =
        "def f() -> int:\n    return 1\ndef g() -> str:\n    s = \"\"\"\n        key: value\n    \"\"\"\n    return s\n";

    [Fact]
    public void BuildIndentMap_SurfacesTheLexersLiteralState()
    {
        IndentationService.BuildIndentMap(D19).LiteralStateUnknown.Should().BeTrue();
        IndentationService.BuildIndentMap(Clean).LiteralStateUnknown.Should().BeFalse();
    }

    // ---- P22f (#2271): a literal lost WITHOUT a lexer abort sets the fact; a lex that loses none does not.
    // The documents are FormattingFallbackTests' constants; the flag names the mechanism that lost it.

    public static TheoryData<string, string, LiteralLoss> LiteralLostWithoutAnAbort => new()
    {
        { "N2", FormattingFallbackTests.N2, LiteralLoss.RePairedCloser },
        { "N4", FormattingFallbackTests.N4, LiteralLoss.DroppedOpener },
        { "X6", FormattingFallbackTests.X6, LiteralLoss.DroppedOpener },
    };

    [Theory]
    [MemberData(nameof(LiteralLostWithoutAnAbort))]
    public void BuildIndentMap_ALiteralLostWithoutAnAbort_IsUnknown(string name, string document, LiteralLoss mechanism)
    {
        IndentationService.BuildIndentMap(document).LiteralStateUnknown.Should().BeTrue(name);

        var lexer = new Lexer(document);
        lexer.TokenizeAll();
        lexer.LiteralLoss.Should().Be(mechanism, "{0} loses its literal by that mechanism alone, without an abort inside a read", name);
    }

    /// <summary>
    /// R-FP (#2273): the compiler's lexer stops at its budget above N1's string (<c>UnreadRemainder</c>); the indent
    /// map's lexer (<see cref="IndentationService.StructureLexer"/>) reads past it, so the map knows the string.
    /// </summary>
    [Fact]
    public void BuildIndentMap_PastTheBudget_ReadsTheString_WhileTheCompilersLexerStops()
    {
        var map = IndentationService.BuildIndentMap(FormattingFallbackTests.N1);
        map.LiteralStateUnknown.Should().BeFalse("the map's lexer has no error budget");
        map.LiteralLines.Should().Contain(FormattingFallbackTests.N1SubLine + 1, "N1's string line is a literal line");

        var lexer = new Lexer(FormattingFallbackTests.N1);
        lexer.TokenizeAll();
        lexer.LiteralLoss.Should().Be(LiteralLoss.UnreadRemainder, "the compiler's lexer still stops at its budget");
    }

    /// <summary>
    /// The map's lexer reads every line of a document with more errors than the compiler's budget: one error per
    /// line, no SPY0905 (the budget's warning), a token on the last line. Positive control: the compiler's lexer on
    /// the same text stops at 25 errors, warns SPY0905, and leaves the last line without a token.
    /// </summary>
    [Fact]
    public void StructureLexer_ReadsPastTheBudget()
    {
        var source = string.Concat(Enumerable.Repeat("x = \"abc\n", 30)) + "y = 1\n";

        var structure = IndentationService.StructureLexer(source);
        var tokens = structure.TokenizeAll();
        structure.Diagnostics.ErrorCount.Should().Be(30);
        structure.Diagnostics.GetWarnings().Should().NotContain(d => d.Code == "SPY0905");
        tokens.Should().Contain(t => t.Line == 31 && t.Type == TokenType.Identifier);

        var compiler = new Lexer(source);
        var compilerTokens = compiler.TokenizeAll();
        compiler.Diagnostics.ErrorCount.Should().Be(compiler.MaxErrors);
        compiler.Diagnostics.GetWarnings().Should().Contain(d => d.Code == "SPY0905");
        compilerTokens.Should().NotContain(t => t.Line == 31 && t.Type == TokenType.Identifier);
    }

    public static TheoryData<string, string> NoLiteralLost => new()
    {
        { "N4q", FormattingFallbackTests.N4q },
        { "N4t", FormattingFallbackTests.N4t },
        { "N1c", FormattingFallbackTests.N1c },
    };

    [Theory]
    [MemberData(nameof(NoLiteralLost))]
    public void BuildIndentMap_ALexThatLosesNoLiteral_IsKnown(string name, string document)
    {
        IndentationService.BuildIndentMap(document).LiteralStateUnknown.Should().BeFalse(name);
    }
}
