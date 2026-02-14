using System.Linq;
using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class DunderInvocationValidatorTests
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
    public void DirectDunderCall_OutsideDunder_ReportsError()
    {
        var code = @"
class Foo:
    def __str__(self) -> str:
        return ""hello""

def bar():
    f: Foo = Foo()
    f.__str__()
";
        var (module, context) = Parse(code);

        var validator = new DunderInvocationValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.DunderDirectInvocation);
    }

    [Fact]
    public void DunderCallOnSelf_InsideDunder_NoError()
    {
        var code = @"
class Foo:
    def __str__(self) -> str:
        return ""hello""

    def __repr__(self) -> str:
        return self.__str__()
";
        var (module, context) = Parse(code);

        var validator = new DunderInvocationValidator();
        validator.Validate(module, context);

        var dunderErrors = context.Diagnostics.GetErrors()
            .Where(e => e.Code == DiagnosticCodes.Validation.DunderDirectInvocation
                     || e.Code == DiagnosticCodes.Validation.DunderWrongReceiver
                     || e.Code == DiagnosticCodes.Validation.DunderCapture)
            .ToList();
        Assert.Empty(dunderErrors);
    }

    [Fact]
    public void DunderCallOnNonSelf_InsideDunder_ReportsWrongReceiver()
    {
        var code = @"
class Foo:
    def __str__(self) -> str:
        return ""hello""

    def __repr__(self) -> str:
        other: Foo = Foo()
        return other.__str__()
";
        var (module, context) = Parse(code);

        var validator = new DunderInvocationValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.DunderWrongReceiver);
    }

    [Fact]
    public void CaptureDunderReference_ReportsCapture()
    {
        var code = @"
class Foo:
    def __str__(self) -> str:
        return ""hello""

def bar():
    f: Foo = Foo()
    x = f.__str__
";
        var (module, context) = Parse(code);

        var validator = new DunderInvocationValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.DunderCapture);
    }

    [Fact]
    public void NormalMethodCall_NoError()
    {
        var code = @"
class Foo:
    def greet(self) -> str:
        return ""hello""

def bar():
    f: Foo = Foo()
    f.greet()
";
        var (module, context) = Parse(code);

        var validator = new DunderInvocationValidator();
        validator.Validate(module, context);

        var dunderErrors = context.Diagnostics.GetErrors()
            .Where(e => e.Code == DiagnosticCodes.Validation.DunderDirectInvocation
                     || e.Code == DiagnosticCodes.Validation.DunderWrongReceiver
                     || e.Code == DiagnosticCodes.Validation.DunderCapture)
            .ToList();
        Assert.Empty(dunderErrors);
    }
}
