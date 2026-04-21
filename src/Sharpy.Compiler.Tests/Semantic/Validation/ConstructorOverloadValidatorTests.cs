using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class ConstructorOverloadValidatorTests
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
    public void DifferentConstructorSignatures_NoDiagnostics()
    {
        var code = @"
class Foo:
    x: int

    def __init__(self):
        self.x = 0

    def __init__(self, x: int):
        self.x = x
";
        var (module, context) = Parse(code);
        var validator = new ConstructorOverloadValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void DuplicateConstructorSignatures_EmitsError()
    {
        var code = @"
class Foo:
    x: int

    def __init__(self, x: int):
        self.x = x

    def __init__(self, y: int):
        self.x = y
";
        var (module, context) = Parse(code);
        var validator = new ConstructorOverloadValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Duplicate constructor signature"));
    }

    [Fact]
    public void SingleConstructor_NoDiagnostics()
    {
        var code = @"
class Foo:
    x: int

    def __init__(self, x: int):
        self.x = x
";
        var (module, context) = Parse(code);
        var validator = new ConstructorOverloadValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }
}
