using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SParser = Sharpy.Compiler.Parser.Parser;
using SModule = Sharpy.Compiler.Parser.Ast.Module;
using SToken = Sharpy.Compiler.Lexer.Token;

namespace Sharpy.Compiler.Tests.Properties.Unparser;

[Trait("Category", "Property")]
public class UnparserFixtureRoundTripTests
{
    private readonly ITestOutputHelper _output;

    public UnparserFixtureRoundTripTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> GetRoundTripFixtures()
    {
        foreach (var fixture in FixtureDiscoveryHelper.DiscoverFixtures())
        {
            if (fixture.ErrorFile != null && fixture.ExpectedFile == null)
                continue;

            if (fixture.IsMultiFile)
                continue;

            if (File.Exists(Path.ChangeExtension(fixture.SpyFilePath, ".skip")))
                continue;

            yield return new object[] { fixture.TestName, fixture.SpyFilePath };
        }
    }

    private static bool UsesPartialApplicationDesugaring(string source)
    {
        return source.Contains("|>") &&
               System.Text.RegularExpressions.Regex.IsMatch(source, @"\w+\(\s*_\s*[,)]");
    }

    [Theory]
    [MemberData(nameof(GetRoundTripFixtures))]
    public void FixtureRoundTrip(string testName, string spyFilePath)
    {
        var source = File.ReadAllText(spyFilePath);
        _output.WriteLine($"Testing: {testName}");

        if (UsesPartialApplicationDesugaring(source))
        {
            _output.WriteLine("  Skipping: uses partial application desugaring (|> with _ placeholder)");
            return;
        }

        var lexer = new SLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new SParser(tokens);
        SModule ast1;
        try
        {
            ast1 = parser.ParseModule();
        }
        catch
        {
            _output.WriteLine("  Parse failed on original source — skipping");
            return;
        }

        if (parser.Diagnostics.HasErrors)
        {
            _output.WriteLine("  Parse produced errors — skipping");
            return;
        }

        var unparsed = Pretty.Unparser.Unparse(ast1);
        _output.WriteLine($"  Unparsed length: {unparsed.Length}");

        var lexer2 = new SLexer(unparsed);
        List<SToken> tokens2;
        try
        {
            tokens2 = lexer2.TokenizeAll();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  Re-lex failed:\n{unparsed}");
            Assert.Fail($"Re-lexing unparsed output failed: {ex.Message}");
            return;
        }

        var parser2 = new SParser(tokens2);
        SModule ast2;
        try
        {
            ast2 = parser2.ParseModule();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  Re-parse failed:\n{unparsed}");
            Assert.Fail($"Re-parsing unparsed output failed: {ex.Message}");
            return;
        }

        if (parser2.Diagnostics.HasErrors)
        {
            _output.WriteLine($"  Re-parse produced errors:\n{unparsed}");
            foreach (var diag in parser2.Diagnostics.GetErrors())
                _output.WriteLine($"    {diag}");
            Assert.Fail("Re-parsing unparsed output produced diagnostics");
        }

        var comparer = Pretty.StructuralEqualityComparer.Instance;
        var normalizer = Pretty.AstNormalizer.Instance;

        var norm1 = normalizer.NormalizeModule(ast1);
        var norm2 = normalizer.NormalizeModule(ast2);

        if (!comparer.Equals(norm1, norm2))
        {
            _output.WriteLine("  AST mismatch after round-trip!");
            _output.WriteLine("--- Original source (first 500 chars) ---");
            _output.WriteLine(source.Length > 500 ? source[..500] : source);
            _output.WriteLine("--- Unparsed output (first 500 chars) ---");
            _output.WriteLine(unparsed.Length > 500 ? unparsed[..500] : unparsed);
            Assert.Fail($"Structural equality failed for {testName}: ASTs differ after parse → unparse → reparse");
        }
    }
}
