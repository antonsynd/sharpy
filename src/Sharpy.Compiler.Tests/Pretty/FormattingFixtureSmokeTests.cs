using FluentAssertions;
using Sharpy.Compiler.Pretty;
using Xunit;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;
using SharpyParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.PrettyTests;

public class FormattingFixtureSmokeTests
{
    private static readonly string FixturesDir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "..", "..", "..", "Integration", "TestFixtures", "Formatting");

    private static string[] GetFormattingFixtures()
    {
        var dir = Path.GetFullPath(FixturesDir);
        if (!Directory.Exists(dir))
            return [];

        return Directory.GetFiles(dir, "*.spy");
    }

    [Fact]
    public void AllFormattingFixtures_ParseAndUnparse_WithoutCrashing()
    {
        var fixtures = GetFormattingFixtures();
        fixtures.Should().NotBeEmpty("formatting fixtures should exist");

        foreach (var fixture in fixtures)
        {
            var source = File.ReadAllText(fixture);
            var name = Path.GetFileNameWithoutExtension(fixture);

            var lexer = new SharpyLexer(source);
            var tokens = lexer.TokenizeAll();
            var parser = new SharpyParser(tokens);
            var module = parser.ParseModule();

            var exception = Record.Exception(() => Unparser.Unparse(module));
            exception.Should().BeNull($"fixture '{name}' should parse and unparse without crashing");
        }
    }

    [Fact]
    public void AllFormattingFixtures_NormalizeAndUnparse_WithoutCrashing()
    {
        var fixtures = GetFormattingFixtures();
        fixtures.Should().NotBeEmpty("formatting fixtures should exist");

        foreach (var fixture in fixtures)
        {
            var source = File.ReadAllText(fixture);
            var name = Path.GetFileNameWithoutExtension(fixture);

            var lexer = new SharpyLexer(source);
            var tokens = lexer.TokenizeAll();
            var parser = new SharpyParser(tokens);
            var module = parser.ParseModule();
            var normalized = AstNormalizer.Instance.NormalizeModule(module);

            var exception = Record.Exception(() => Unparser.Unparse(normalized));
            exception.Should().BeNull($"fixture '{name}' should normalize and unparse without crashing");
        }
    }

    /// <summary>
    /// The shapes whose normalizer arm was missing (#1974 P21a gate red: the <c>let_declarations</c>
    /// fixture's <c>@suppress</c>'d statement). Keywordless on purpose — the crash was never about
    /// <c>let</c>: the normalizer returned null for a <c>DecoratedStatement</c> and the unparser
    /// dereferenced it. Each source is normalized, unparsed, and re-parsed to the same normalized
    /// tree, so a "fix" that returned a placeholder node would still be red.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    @suppress(SPY0451)\n    unused = 9\n")]
    [InlineData("def main():\n    @suppress(SPY0451)\n    let unused = 9\n")]
    [InlineData("def f(r: Result[int, str]) -> Result[int, str]:\n    return Ok(r? + 1)\n")]
    public void FormerlyUnarmedShapes_NormalizeUnparseAndReparseEqual(string source)
    {
        var module = new SharpyParser(new SharpyLexer(source).TokenizeAll()).ParseModule();
        var normalized = AstNormalizer.Instance.NormalizeModule(module);

        var text = Unparser.Unparse(normalized);

        var reparsed = new SharpyParser(new SharpyLexer(text).TokenizeAll()).ParseModule();
        StructuralEqualityComparer.Instance
            .Equals(normalized, AstNormalizer.Instance.NormalizeModule(reparsed))
            .Should().BeTrue($"the unparsed text must re-parse to the same tree:\n{text}");
    }
}
