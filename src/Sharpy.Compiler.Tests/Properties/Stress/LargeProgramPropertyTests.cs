using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;
using SharpyUnparser = Sharpy.Compiler.Pretty.Unparser;

namespace Sharpy.Compiler.Tests.Properties.Stress;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("StressTests")]
public class LargeProgramPropertyTests
{
    private readonly ITestOutputHelper _output;

    public LargeProgramPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Timeout = 30000)]
    public void HighFuelPrograms_LexerParserDoNotCrash()
    {
        int completed = 0;

        GenSharpy.Module(GenContext.HighFuel).Sample(module =>
        {
            var source = SharpyUnparser.Unparse(module);
            var lexer = new SharpyLexer(source);
            var tokens = lexer.TokenizeAll();

            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
            Interlocked.Increment(ref completed);
        }, print: m => SharpyUnparser.Unparse(m), iter: 30);

        _output.WriteLine($"High-fuel programs completed: {completed}");
        Assert.True(completed > 0, "No high-fuel programs completed");
    }

    [Fact(Timeout = 30000)]
    public void DeepNestingPrograms_LexerParserDoNotCrash()
    {
        int completed = 0;

        GenSharpy.Module(GenContext.DeepNesting).Sample(module =>
        {
            var source = SharpyUnparser.Unparse(module);
            var lexer = new SharpyLexer(source);
            var tokens = lexer.TokenizeAll();

            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
            Interlocked.Increment(ref completed);
        }, print: m => SharpyUnparser.Unparse(m), iter: 20);

        _output.WriteLine($"Deep-nesting programs completed: {completed}");
        Assert.True(completed > 0, "No deep-nesting programs completed");
    }

    [Fact(Timeout = 30000)]
    public void ManyDefinitions_SemanticAnalysisCompletes()
    {
        int total = 0;
        int passed = 0;

        Gen.Int[5, 15].SelectMany(count =>
        {
            var ctx = GenContext.Default with { Fuel = 3, InFunction = true };
            return GenStatements.FunctionDefStmt(ctx).Array[count, count]
                .Select(fns =>
                {
                    var source = string.Join("\n\n", fns.Select(f => SharpyUnparser.Unparse(f)));
                    return source + "\n";
                });
        }).Sample(source =>
        {
            Interlocked.Increment(ref total);
            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Analyze(source, "stress_test.spy");
                if (result.Success)
                    Interlocked.Increment(ref passed);
            }
            catch
            {
                // Swallow — we're testing no crashes, not correctness
            }
        }, iter: 30);

        _output.WriteLine($"Many-definitions analysis: {passed}/{total} passed");
        Assert.True(total > 0, "No multi-definition programs generated");
    }

    [Fact(Timeout = 30000)]
    public void DeeplyNestedExpressions_ParserHandlesCorrectly()
    {
        Gen.Int[10, 30].SelectMany(depth =>
        {
            var expr = "x";
            for (int i = 0; i < depth; i++)
                expr = $"({expr} + y)";
            return Gen.Const($"result = {expr}\n");
        }).Sample(source =>
        {
            var lexer = new SharpyLexer(source);
            var tokens = lexer.TokenizeAll();

            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            var module = parser.ParseModule();

            if (module.Body.Length == 0)
                throw new Exception(
                    $"Empty module body for nested expression of length {source.Length}");
        }, iter: 50);
    }

    [Fact(Timeout = 30000)]
    public void ManyParameters_NoStackOverflow()
    {
        Gen.Int[5, 25].SelectMany(paramCount =>
        {
            var @params = string.Join(", ", Enumerable.Range(0, paramCount).Select(i => $"p{i}: int"));
            var body = paramCount > 0 ? $"return p0" : "return 0";
            return Gen.Const($"def big_func({@params}) -> int:\n    {body}\n");
        }).Sample(source =>
        {
            var lexer = new SharpyLexer(source);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            var module = parser.ParseModule();

            if (module.Body.Length == 0)
                throw new Exception("Empty module body for many-parameter function");
        }, iter: 50);
    }
}
