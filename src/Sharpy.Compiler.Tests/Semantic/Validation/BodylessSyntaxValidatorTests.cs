using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class BodylessSyntaxValidatorTests
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
    public void BodylessMethodDeclaration_EmitsDeprecationWarning()
    {
        var code = @"
@abstract
class Animal:
    @abstract
    def speak(self) -> str
";
        var (module, context) = Parse(code);
        var validator = new BodylessSyntaxValidator();
        validator.Validate(module, context);

        var warnings = context.Diagnostics.GetWarnings();
        Assert.Contains(warnings, w => w.Message.Contains("Body-less method declaration") && w.Message.Contains("speak"));
        Assert.Contains(warnings, w => w.Message.Contains("deprecated"));
    }

    [Fact]
    public void ExplicitEllipsisBody_NoWarning()
    {
        var code = @"
@abstract
class Animal:
    @abstract
    def speak(self) -> str: ...
";
        var (module, context) = Parse(code);
        var validator = new BodylessSyntaxValidator();
        validator.Validate(module, context);

        var warnings = context.Diagnostics.GetWarnings();
        Assert.DoesNotContain(warnings, w => w.Message.Contains("Body-less method declaration"));
    }

    [Fact]
    public void NormalMethodWithBody_NoWarning()
    {
        var code = @"
class Foo:
    def greet(self) -> str:
        return ""hello""
";
        var (module, context) = Parse(code);
        var validator = new BodylessSyntaxValidator();
        validator.Validate(module, context);

        var warnings = context.Diagnostics.GetWarnings();
        Assert.DoesNotContain(warnings, w => w.Message.Contains("Body-less method declaration"));
    }
}
