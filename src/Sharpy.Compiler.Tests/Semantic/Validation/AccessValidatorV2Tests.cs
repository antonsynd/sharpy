using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class AccessValidatorV2Tests
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

        // Run name resolution
        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        // Run type checking to populate semantic info
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.CheckModule(module, isEntryPoint: true);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    [Fact]
    public void PublicMember_AccessibleFromOutside()
    {
        var code = @"
class Foo:
    def public_method(self) -> None:
        pass

def test() -> None:
    f: Foo = Foo()
    f.public_method()
";
        var (module, context) = Parse(code);

        var validator = new AccessValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void PrivateMember_AccessibleFromSameClass()
    {
        var code = @"
class Foo:
    __secret: int = 42

    def reveal(self) -> int:
        return self.__secret
";
        var (module, context) = Parse(code);

        var validator = new AccessValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void PrivateMember_NotAccessibleFromOutside()
    {
        var code = @"
class Foo:
    __secret: int = 42

def test() -> None:
    f: Foo = Foo()
    x: int = f.__secret
";
        var (module, context) = Parse(code);

        var validator = new AccessValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Cannot access private member"));
    }

    [Fact]
    public void ProtectedMember_AccessibleFromSameClass()
    {
        var code = @"
class Foo:
    _internal: int = 42

    def use_internal(self) -> int:
        return self._internal
";
        var (module, context) = Parse(code);

        var validator = new AccessValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ProtectedMember_AccessibleFromSubclass()
    {
        var code = @"
class Base:
    _internal: int = 42

class Child(Base):
    def use_internal(self) -> int:
        return self._internal
";
        var (module, context) = Parse(code);

        var validator = new AccessValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ProtectedMember_NotAccessibleFromOutside()
    {
        var code = @"
class Foo:
    _internal: int = 42

def test() -> None:
    f: Foo = Foo()
    x: int = f._internal
";
        var (module, context) = Parse(code);

        var validator = new AccessValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Cannot access protected member"));
    }

    [Fact]
    public void DunderMethod_IsPublic()
    {
        // Dunder methods (__init__, __str__, etc.) are public, not private
        var code = @"
class Foo:
    def __init__(self) -> None:
        pass

    def __str__(self) -> str:
        return ""Foo""

def test() -> None:
    f: Foo = Foo()
    s: str = f.__str__()
";
        var (module, context) = Parse(code);

        var validator = new AccessValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void PrivateMethod_NotAccessibleFromOutside()
    {
        var code = @"
class Foo:
    def __private_helper(self) -> None:
        pass

def test() -> None:
    f: Foo = Foo()
    f.__private_helper()
";
        var (module, context) = Parse(code);

        var validator = new AccessValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Cannot access private member"));
    }

    [Fact]
    public void StructMember_AccessValidation()
    {
        var code = @"
struct Point:
    x: int
    y: int
    __internal: int = 0

def test() -> None:
    p: Point = Point(1, 2)
    a: int = p.x
    b: int = p.__internal
";
        var (module, context) = Parse(code);

        var validator = new AccessValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Cannot access private member") && e.Message.Contains("__internal"));
    }

    [Fact]
    public void PrivateMethod_AccessibleFromSameClass()
    {
        var code = @"
class Foo:
    def __private_helper(self) -> int:
        return 42

    def public_method(self) -> int:
        return self.__private_helper()
";
        var (module, context) = Parse(code);

        var validator = new AccessValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }
}
