using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Parser;

/// <summary>
/// Round-trip coverage for the <c>defer</c> statement (#1075). The unparser previously had no
/// <c>VisitDeferStatement</c>, so a module containing a defer lost its structure (the base
/// DefaultVisit recursed into the body and dropped the <c>defer</c> wrapper). This generates
/// both inline (<c>defer f()</c>) and block (<c>defer:</c> suite) forms inside a function body
/// and asserts the parse → unparse → reparse round-trip is structurally stable.
///
/// The defer feature is gated at semantic analysis, but this property is purely syntactic
/// (parse → unparse → reparse), so no feature flag is needed.
/// </summary>
[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class DeferRoundTripPropertyTests
{
    private readonly ITestOutputHelper _output;

    public DeferRoundTripPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static Gen<Module> DeferModule =>
        GenStatements.DeferStmt(GenContext.Default).Array[1, 3].Select(defers =>
        {
            var body = defers.Cast<Statement>().ToImmutableArray();
            var fn = new FunctionDef { Name = "f", Body = body };
            return new Module { Body = ImmutableArray.Create<Statement>(fn) };
        });

    [Fact]
    public void DeferStatements_RoundTripStructurally()
    {
        DeferModule.Sample(module =>
        {
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            var tokens = new Sharpy.Compiler.Lexer.Lexer(unparsed).TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            var reparsed = parser.ParseModule();
            Assert.False(parser.Diagnostics.HasErrors,
                $"Reparsing unparsed defer failed:\n{unparsed}");

            var normalizer = Sharpy.Compiler.Pretty.AstNormalizer.Instance;
            var norm1 = normalizer.NormalizeModule(module);
            var norm2 = normalizer.NormalizeModule(reparsed);
            Assert.True(Sharpy.Compiler.Pretty.StructuralEqualityComparer.Instance.Equals(norm1, norm2),
                $"defer round-trip changed the AST.\nUnparsed:\n{unparsed}");
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 200);
    }

    [Theory]
    [InlineData("defer print(\"cleanup\")")]
    [InlineData("defer x = 1")]
    public void InlineDefer_RoundTrips(string deferLine)
    {
        var source = "def f() -> None:\n    " + deferLine + "\n    pass\n";
        AssertRoundTrips(source);
    }

    [Fact]
    public void BlockDefer_RoundTrips()
    {
        var source = "def f() -> None:\n    defer:\n        print(\"a\")\n        print(\"b\")\n    pass\n";
        AssertRoundTrips(source);
    }

    private void AssertRoundTrips(string source)
    {
        var tokens1 = new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll();
        var parser1 = new Sharpy.Compiler.Parser.Parser(tokens1);
        var ast1 = parser1.ParseModule();
        Assert.False(parser1.Diagnostics.HasErrors, $"Original defer source failed to parse:\n{source}");

        var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(ast1);
        _output.WriteLine(unparsed);

        var tokens2 = new Sharpy.Compiler.Lexer.Lexer(unparsed).TokenizeAll();
        var parser2 = new Sharpy.Compiler.Parser.Parser(tokens2);
        var ast2 = parser2.ParseModule();
        Assert.False(parser2.Diagnostics.HasErrors, $"Reparse failed:\n{unparsed}");

        var normalizer = Sharpy.Compiler.Pretty.AstNormalizer.Instance;
        Assert.True(
            Sharpy.Compiler.Pretty.StructuralEqualityComparer.Instance.Equals(
                normalizer.NormalizeModule(ast1), normalizer.NormalizeModule(ast2)),
            $"defer round-trip changed the AST.\nOriginal:\n{source}\nUnparsed:\n{unparsed}");
    }
}
