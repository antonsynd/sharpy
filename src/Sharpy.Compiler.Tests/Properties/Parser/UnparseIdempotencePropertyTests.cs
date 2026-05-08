using CsCheck;
using Sharpy.Compiler.Pretty;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Parser;

[Trait("Category", "Property")]
public class UnparseIdempotencePropertyTests
{
    private readonly ITestOutputHelper _output;

    public UnparseIdempotencePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Unparse_IsIdempotent_AtStringLevel()
    {
        Gen.Int[1, 4].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var pass1 = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            var lexer = new Sharpy.Compiler.Lexer.Lexer(pass1);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            if (parser.Diagnostics.HasErrors)
                return;

            var reparsed = parser.ParseModule();
            if (parser.Diagnostics.HasErrors)
                return;

            var pass2 = Sharpy.Compiler.Pretty.Unparser.Unparse(reparsed);

            if (pass1 != pass2)
            {
                _output.WriteLine("--- Pass 1 ---");
                _output.WriteLine(pass1.Length > 500 ? pass1[..500] : pass1);
                _output.WriteLine("--- Pass 2 ---");
                _output.WriteLine(pass2.Length > 500 ? pass2[..500] : pass2);
                throw new Exception(
                    "Unparse is not idempotent: unparse(parse(unparse(ast))) != unparse(ast)");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }
}
