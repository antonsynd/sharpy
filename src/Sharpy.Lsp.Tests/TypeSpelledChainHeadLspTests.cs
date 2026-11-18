using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;
using static Sharpy.Lsp.Handlers.SharpySemanticTokensHandler;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// P93 D4 (plan-67a01f, T2's LSP check): <c>c is Color.RED is d</c> is a three-operand
/// <see cref="Compiler.Parser.Ast.ComparisonChain"/> with <c>is</c> first and the head's type spelling in
/// <c>HeadCheckType</c>. The editor walks the chain's operands and operator positions, so both <c>is</c>
/// keywords are colored (parse-only token collection, so only syntax tokens), and hover over the
/// chain's operands resolves as it does outside a chain. <c>FrontEndParityTests</c> compares diagnostic multisets only, so it does not hold this.
/// </summary>
public class TypeSpelledChainHeadLspTests
{
    private const string Source =
        "enum Color:\n" +
        "    RED = 0\n" +
        "    BLUE = 1\n" +
        "def main():\n" +
        "    c = Color.RED\n" +
        "    d = Color.RED\n" +
        "    print(c is Color.RED is d)\n";

    // 0-based line of the chain, and the 0-based columns on it.
    private const int ChainLine = 6;
    private const int FirstIsColumn = 12;
    private const int SecondIsColumn = 25;
    private const int RedColumn = 21;

    private readonly CompilerApi _api = new();

    [Fact]
    public void SemanticTokens_ColorBothIsKeywords()
    {
        var parseResult = _api.Parse(Source);
        parseResult.Success.Should().BeTrue();
        var tokens = new System.Collections.Generic.List<RawToken>();
        SharpySemanticTokensHandler.CollectTokens(parseResult.Ast!.Body, tokens);

        var onLine = tokens.Where(t => t.Line == ChainLine).OrderBy(t => t.Col).ToList();
        onLine.Should().Contain(t => t.TokenType == TKeyword && t.Col == FirstIsColumn && t.Length == 2,
            "the head's `is` is the chain's first link, colored from its recorded position");
        onLine.Should().Contain(t => t.TokenType == TKeyword && t.Col == SecondIsColumn && t.Length == 2);
        for (var i = 1; i < onLine.Count; i++)
            onLine[i].Col.Should().BeGreaterThanOrEqualTo(onLine[i - 1].Col + onLine[i - 1].Length, "tokens on one line never overlap");
    }

    [Fact]
    public void Hover_OverTheChainOperands_Resolves()
    {
        var result = _api.Analyze(Source);
        var hoverService = new HoverService(_api);

        // 1-based positions: `d` (the last operand) and `RED` (the identity operand).
        var overD = hoverService.GetHoverMarkdown(result, ChainLine + 1, SecondIsColumn + 4);
        overD.Should().NotBeNull();
        overD.Should().Contain("Color");

        // The identity operand hovers exactly as the same member written outside a chain
        // (`d = Color.RED`, line 6, `RED` at 1-based column 15).
        var overRed = hoverService.GetHoverMarkdown(result, ChainLine + 1, RedColumn + 1);
        var overRedOutsideTheChain = hoverService.GetHoverMarkdown(result, 6, 15);
        overRedOutsideTheChain.Should().NotBeNull().And.Contain("RED");
        overRed.Should().Be(overRedOutsideTheChain);
    }
}
