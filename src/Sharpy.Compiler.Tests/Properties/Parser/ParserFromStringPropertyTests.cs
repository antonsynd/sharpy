using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Parser;

[Trait("Category", "Property")]
public class ParserFromStringPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ParserFromStringPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void WhitespaceVariation_NeverCrashesParser()
    {
        Gen.Int[1, 3].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var modified = InsertRandomWhitespace(source);

            var lexer = new Sharpy.Compiler.Lexer.Lexer(modified);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }

    [Fact]
    public void WhitespaceVariation_DiagnosticsAreDeterministic()
    {
        Gen.Int[1, 3].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var modified = InsertTrailingSpaces(source);

            var diags1 = ParseAndGetDiagnostics(modified);
            var diags2 = ParseAndGetDiagnostics(modified);

            if (diags1.Count != diags2.Count)
                throw new Exception(
                    $"Non-deterministic diagnostics: {diags1.Count} vs {diags2.Count}");

            for (int i = 0; i < diags1.Count; i++)
            {
                if (diags1[i] != diags2[i])
                    throw new Exception(
                        $"Diagnostic #{i} differs: '{diags1[i]}' vs '{diags2[i]}'");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }

    private static string InsertRandomWhitespace(string source)
    {
        var lines = source.Split('\n');
        var result = new List<string>();
        foreach (var line in lines)
        {
            result.Add(line.TrimEnd() + "   ");
        }
        return string.Join('\n', result);
    }

    private static string InsertTrailingSpaces(string source)
    {
        var lines = source.Split('\n');
        return string.Join('\n', lines.Select(l => l.TrimEnd() + "  "));
    }

    private static List<string> ParseAndGetDiagnostics(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        _ = parser.ParseModule();
        return parser.Diagnostics.GetAll().Select(d => d.ToString()!).ToList();
    }
}
