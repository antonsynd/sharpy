using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Parser;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class CompoundStatementRoundTripTests
{
    private readonly ITestOutputHelper _output;

    public CompoundStatementRoundTripTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void LinearProgram_NeverCrashesParser()
    {
        GenShape.LinearProgram(GenContext.Default).Sample(module =>
        {
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(unparsed);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }

    [Fact]
    public void ClassHierarchy_NeverCrashesParser()
    {
        GenShape.ClassHierarchy(GenContext.Default).Sample(module =>
        {
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(unparsed);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }

    [Fact]
    public void ComprehensionProgram_NeverCrashesParser()
    {
        GenShape.ComprehensionProgram(GenContext.Default).Sample(module =>
        {
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(unparsed);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }

    [Fact]
    public void ImportProgram_NeverCrashesParser()
    {
        GenShape.ImportProgram(GenContext.Default).Sample(module =>
        {
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(unparsed);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }

    [Fact]
    public void EnumProgram_NeverCrashesParser()
    {
        GenShape.EnumProgram(GenContext.Default).Sample(module =>
        {
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(unparsed);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }

    // Cast round-trip axis (#1096): TypeCoercionExpr now always mixes the legacy `to`/`to?` forms
    // with the graduated `as!`/`as?` forms, so the round trip exercises both spellings through
    // unparse and reparse. Parsing was never gated (the retired failable_cast gate lived in
    // semantic analysis), so no feature enablement is needed here.
    [Fact]
    public void LinearProgram_WithFailableCast_NeverCrashesParser()
    {
        GenShape.LinearProgram(GenContext.Default).Sample(module =>
        {
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(unparsed);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }

    [Fact]
    public void ClassHierarchy_WithFailableCast_NeverCrashesParser()
    {
        GenShape.ClassHierarchy(GenContext.Default).Sample(module =>
        {
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(unparsed);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }
}
