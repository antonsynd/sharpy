using System.Linq;
using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Parser tests for the <c>defer</c> statement (#1023). The statement is always parsed
/// regardless of the experimental feature flag; gating is enforced later in semantic
/// analysis, so these tests assert only on the produced AST shape.
/// </summary>
public class DeferParserTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            "defer source should parse cleanly: "
            + string.Join("\n", parser.Diagnostics.GetErrors().Select(d => d.Message)));
        return module;
    }

    private static DeferStatement FirstDefer(Module module)
    {
        var func = module.Body.OfType<FunctionDef>().First();
        return func.Body.OfType<DeferStatement>().First();
    }

    [Fact]
    public void InlineForm_ParsesSingleStatementBody()
    {
        var module = Parse("def main() -> None:\n    defer f.close()\n    pass\n");
        var defer = FirstDefer(module);

        defer.IsBlock.Should().BeFalse();
        defer.Body.Should().ContainSingle();
        defer.Body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<FunctionCall>();
    }

    [Fact]
    public void BlockForm_ParsesIndentedSuite()
    {
        var module = Parse("def main() -> None:\n    defer:\n        print(1)\n        print(2)\n    pass\n");
        var defer = FirstDefer(module);

        defer.IsBlock.Should().BeTrue();
        defer.Body.Should().HaveCount(2);
        defer.Body.Should().AllBeOfType<ExpressionStatement>();
    }

    [Fact]
    public void InlineForm_ChildNodesExposeBody()
    {
        var module = Parse("def main() -> None:\n    defer cleanup()\n    pass\n");
        var defer = FirstDefer(module);

        defer.GetChildNodes().Should().ContainSingle()
            .Which.Should().BeSameAs(defer.Body[0]);
    }

    [Fact]
    public void MultipleDefers_ParseInOrder()
    {
        var module = Parse("def main() -> None:\n    defer a()\n    defer b()\n    pass\n");
        var func = module.Body.OfType<FunctionDef>().First();
        var defers = func.Body.OfType<DeferStatement>().ToList();

        defers.Should().HaveCount(2);
    }

    [Fact]
    public void BlockForm_MissingSuite_ReportsError()
    {
        // `defer:` with no indented body is a syntax error (empty block).
        var lexer = new LexerNs.Lexer("def main() -> None:\n    defer:\n    pass\n");
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        parser.ParseModule();

        parser.Diagnostics.HasErrors.Should().BeTrue(
            "an empty `defer:` block should be rejected by the parser");
    }
}
