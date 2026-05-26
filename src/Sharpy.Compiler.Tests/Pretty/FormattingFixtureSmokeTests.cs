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
}
