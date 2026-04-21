using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class StructRulesValidatorTests
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
    public void StructConstructorInitializesAllFields_NoDiagnostics()
    {
        var code = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
";
        var (module, context) = Parse(code);
        var validator = new StructRulesValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void StructConstructorMissingFieldInit_EmitsError()
    {
        var code = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int):
        self.x = x
";
        var (module, context) = Parse(code);
        var validator = new StructRulesValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must initialize all fields"));
    }

    [Fact]
    public void StructWithoutInit_NoDiagnostics()
    {
        var code = @"
struct Pair:
    x: int
    y: int
";
        var (module, context) = Parse(code);
        var validator = new StructRulesValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }
}
