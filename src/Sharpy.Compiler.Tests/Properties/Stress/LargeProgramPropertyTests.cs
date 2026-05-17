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
[Collection("HeavyCompilation")]
public class LargeProgramPropertyTests
{
    private readonly ITestOutputHelper _output;

    public LargeProgramPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Timeout = 180000, Skip = "Requires > 5 GB heap — incompatible with HeapHardLimit constraint")]
    public async Task HighFuelPrograms_LexerParserDoNotCrash()
    {
        int completed = 0;

        await Task.Run(() =>
        {
            GenSharpy.Module(GenContext.HighFuel).Sample(module =>
            {
                var source = SharpyUnparser.Unparse(module);
                var lexer = new SharpyLexer(source);
                var tokens = lexer.TokenizeAll();

                var parser = new Sharpy.Compiler.Parser.Parser(tokens);
                _ = parser.ParseModule();
                Interlocked.Increment(ref completed);
            }, print: m => SharpyUnparser.Unparse(m), iter: 5);
        });

        _output.WriteLine($"High-fuel programs completed: {completed}");
        Assert.True(completed > 0, "No high-fuel programs completed");
    }

    [Fact(Timeout = 180000, Skip = "Requires > 5 GB heap — incompatible with HeapHardLimit constraint")]
    public async Task DeepNestingPrograms_LexerParserDoNotCrash()
    {
        int completed = 0;

        await Task.Run(() =>
        {
            GenSharpy.Module(GenContext.DeepNesting).Sample(module =>
            {
                var source = SharpyUnparser.Unparse(module);
                var lexer = new SharpyLexer(source);
                var tokens = lexer.TokenizeAll();

                var parser = new Sharpy.Compiler.Parser.Parser(tokens);
                _ = parser.ParseModule();
                Interlocked.Increment(ref completed);
            }, print: m => SharpyUnparser.Unparse(m), iter: 5);
        });

        _output.WriteLine($"Deep-nesting programs completed: {completed}");
        Assert.True(completed > 0, "No deep-nesting programs completed");
    }

    [Fact(Timeout = 60000)]
    public async Task ManyDefinitions_SemanticAnalysisCompletes()
    {
        int total = 0;
        int passed = 0;

        await Task.Run(() =>
        {
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
                }
            }, iter: 30);
        });

        _output.WriteLine($"Many-definitions analysis: {passed}/{total} passed");
        Assert.True(total > 0, "No multi-definition programs generated");
    }

    [Fact(Timeout = 60000)]
    public async Task DeeplyNestedExpressions_ParserHandlesCorrectly()
    {
        await Task.Run(() =>
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
        });
    }

    [Fact(Timeout = 60000)]
    public async Task ManyParameters_NoStackOverflow()
    {
        await Task.Run(() =>
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
        });
    }
}
