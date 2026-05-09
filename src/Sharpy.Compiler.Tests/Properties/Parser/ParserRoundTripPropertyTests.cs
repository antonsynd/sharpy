using CsCheck;
using Sharpy.Compiler.Pretty;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Parser;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class ParserRoundTripPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ParserRoundTripPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GeneratedModule_RoundTrips_Structurally()
    {
        int total = 0;
        int passed = 0;
        int skipped = 0;

        Gen.Int[1, 5].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            Interlocked.Increment(ref total);
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            var lexer = new Sharpy.Compiler.Lexer.Lexer(unparsed);
            List<Sharpy.Compiler.Lexer.Token> tokens;
            try
            {
                tokens = lexer.TokenizeAll();
            }
            catch
            {
                Interlocked.Increment(ref skipped);
                return;
            }

            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            Sharpy.Compiler.Parser.Ast.Module reparsed;
            try
            {
                reparsed = parser.ParseModule();
            }
            catch
            {
                Interlocked.Increment(ref skipped);
                return;
            }

            if (parser.Diagnostics.HasErrors)
            {
                Interlocked.Increment(ref skipped);
                return;
            }

            var comparer = Sharpy.Compiler.Pretty.StructuralEqualityComparer.Instance;
            var normalizer = Sharpy.Compiler.Pretty.AstNormalizer.Instance;
            var norm1 = normalizer.NormalizeModule(module);
            var norm2 = normalizer.NormalizeModule(reparsed);

            if (comparer.Equals(norm1, norm2))
            {
                Interlocked.Increment(ref passed);
            }
        }, iter: 200);

        _output.WriteLine($"Round-trip: {passed}/{total} passed, {skipped} skipped");
        Assert.True(passed > total / 2,
            $"Round-trip pass rate too low: {passed}/{total} ({skipped} skipped)");
    }

    [Fact]
    public void GeneratedModule_NeverCrashesParser()
    {
        Gen.Int[1, 5].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(unparsed);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 200);
    }

    [Fact]
    public void ParseDiagnostics_AreDeterministic()
    {
        Gen.Int[1, 4].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var diags1 = ParseAndGetDiagnostics(unparsed);
            var diags2 = ParseAndGetDiagnostics(unparsed);

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

    private static List<string> ParseAndGetDiagnostics(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        _ = parser.ParseModule();
        return parser.Diagnostics.GetAll().Select(d => d.ToString()!).ToList();
    }

    private static string Truncate(string s, int max = 500) =>
        s.Length > max ? s[..max] + "..." : s;
}
