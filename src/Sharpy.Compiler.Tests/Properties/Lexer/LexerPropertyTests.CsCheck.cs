using CsCheck;
using Sharpy.Compiler.Pretty;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Lexer;

[Trait("Category", "Property")]
public class LexerCsCheckPropertyTests
{
    private readonly ITestOutputHelper _output;

    public LexerCsCheckPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TokenPositions_AreMonotonicallyIncreasing()
    {
        Gen.Int[1, 4].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(source);
            var tokens = lexer.TokenizeAll();

            int lastEnd = 0;
            foreach (var token in tokens)
            {
                if (token.Type == Sharpy.Compiler.Lexer.TokenType.Eof)
                    break;
                if (token.Type == Sharpy.Compiler.Lexer.TokenType.Newline ||
                    token.Type == Sharpy.Compiler.Lexer.TokenType.Indent ||
                    token.Type == Sharpy.Compiler.Lexer.TokenType.Dedent)
                    continue;

                if (token.Position >= 0 && token.Position < lastEnd)
                    throw new Exception(
                        $"Token positions not monotonic: {token.Type} at {token.Position} < {lastEnd}");

                if (token.Position >= 0)
                    lastEnd = token.Position + token.Length;
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 200);
    }

    [Fact]
    public void Lexer_NeverCrashes_OnGeneratedSource()
    {
        Gen.Int[1, 5].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(source);
            _ = lexer.TokenizeAll();
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 200);
    }

    [Fact]
    public void Tokenization_IsDeterministic()
    {
        Gen.Int[1, 4].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var tokens1 = new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll();
            var tokens2 = new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll();

            if (tokens1.Count != tokens2.Count)
                throw new Exception(
                    $"Non-deterministic token count: {tokens1.Count} vs {tokens2.Count}");

            for (int i = 0; i < tokens1.Count; i++)
            {
                if (tokens1[i].Type != tokens2[i].Type)
                    throw new Exception(
                        $"Token #{i} type differs: {tokens1[i].Type} vs {tokens2[i].Type}");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }
}
