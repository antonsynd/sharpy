using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class EnumRulesValidatorTests
{
    private (Module module, SemanticContext context) Parse(string code)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    [Fact]
    public void ValidEnumWithConsistentIntValues_NoDiagnostics()
    {
        var code = @"
enum Color:
    RED = 0
    GREEN = 1
    BLUE = 2
";
        var (module, context) = Parse(code);
        var validator = new EnumRulesValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ValidEnumWithConsistentStrValues_NoDiagnostics()
    {
        var code = @"
enum Direction:
    NORTH = ""north""
    SOUTH = ""south""
    EAST = ""east""
    WEST = ""west""
";
        var (module, context) = Parse(code);
        var validator = new EnumRulesValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void EnumWithMixedTypes_EmitsError()
    {
        var code = @"
enum Mixed:
    A = 1
    B = ""two""
";
        var (module, context) = Parse(code);
        var validator = new EnumRulesValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("same type"));
    }
}
