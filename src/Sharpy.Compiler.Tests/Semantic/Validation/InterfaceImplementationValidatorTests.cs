using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class InterfaceImplementationValidatorTests
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
        var semanticBinding = new SemanticBinding();

        var nameResolver = new NameResolver(symbolTable, semanticBinding: semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.SemanticBinding = semanticBinding;
        typeChecker.CheckModule(module, isEntryPoint: false);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        context.SemanticBinding = semanticBinding;
        return (module, context);
    }

    [Fact]
    public void ClassImplementsAllInterfaceMethods_NoDiagnostics()
    {
        var code = @"
interface Drawable:
    def draw(self) -> str:
        ...

class Circle(Drawable):
    def draw(self) -> str:
        return ""circle""
";
        var (module, context) = Parse(code);
        var validator = new InterfaceImplementationValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ClassMissingInterfaceMethod_EmitsError()
    {
        var code = @"
interface Drawable:
    def draw(self) -> str:
        ...

class Circle(Drawable):
    def area(self) -> float:
        return 3.14
";
        var (module, context) = Parse(code);
        var validator = new InterfaceImplementationValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("does not implement interface method"));
    }

    [Fact]
    public void ClassWithWrongParameterCount_EmitsError()
    {
        var code = @"
interface Computable:
    def compute(self, x: int, y: int) -> int:
        ...

class Adder(Computable):
    def compute(self, x: int) -> int:
        return x
";
        var (module, context) = Parse(code);
        var validator = new InterfaceImplementationValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("parameters"));
    }

    [Fact]
    public void AbstractClassSkipsEnforcement_NoDiagnostics()
    {
        var code = @"
interface Drawable:
    def draw(self) -> str:
        ...

@abstract
class Shape(Drawable):
    def area(self) -> float:
        return 0.0
";
        var (module, context) = Parse(code);
        var validator = new InterfaceImplementationValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void MultipleInterfaces_EachValidated()
    {
        var code = @"
interface Drawable:
    def draw(self) -> str:
        ...

interface Printable:
    def to_string(self) -> str:
        ...

class Widget(Drawable, Printable):
    def draw(self) -> str:
        return ""widget""
";
        var (module, context) = Parse(code);
        var validator = new InterfaceImplementationValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("does not implement interface method") && e.Message.Contains("to_string"));
    }

    [Fact]
    public void StructMissingInterfaceMethod_EmitsError()
    {
        var code = @"
interface Describable:
    def describe(self) -> str:
        ...

struct Point(Describable):
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
";
        var (module, context) = Parse(code);
        var validator = new InterfaceImplementationValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("does not implement interface method") && e.Message.Contains("describe"));
    }

    [Fact]
    public void StructImplementsAllInterfaceMethods_NoDiagnostics()
    {
        var code = @"
interface Describable:
    def describe(self) -> str:
        ...

struct Point(Describable):
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def describe(self) -> str:
        return ""point""
";
        var (module, context) = Parse(code);
        var validator = new InterfaceImplementationValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }
}
