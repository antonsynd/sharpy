using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class AccessValidatorTests
{
    private readonly ITestOutputHelper _output;

    public AccessValidatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

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
        typeChecker.CheckModule(module, isEntryPoint: false);

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

        var validator = new AccessValidator();
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

        var validator = new AccessValidator();
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

        var validator = new AccessValidator();
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

        var validator = new AccessValidator();
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

        var validator = new AccessValidator();
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

        var validator = new AccessValidator();
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

        var validator = new AccessValidator();
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

        var validator = new AccessValidator();
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

        var validator = new AccessValidator();
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

        var validator = new AccessValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void CrossModule_SameNameTypes_ProtectedAccessDenied()
    {
        // Two modules each define a class named "Base". A subclass of module_a.Base
        // should NOT get protected access to module_b.Base's members just because
        // the class names happen to match.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("CrossModuleAccessTest")
            .AddSourceFile("module_a.spy", @"
class Base:
    _value: int

    def __init__(self):
        self._value = 42
")
            .AddSourceFile("module_b.spy", @"
class Base:
    _data: str

    def __init__(self):
        self._data = ""hello""
")
            .AddSourceFile("main.spy", @"
from module_a import Base as BaseA
from module_b import Base as BaseB

class ChildA(BaseA):
    def get_value(self) -> int:
        return self._value  # OK - actual subclass of module_a.Base

class ChildB(BaseB):
    def try_access(self, a: BaseA) -> int:
        return a._value  # ERROR - not in module_a.Base's hierarchy
")
            .CreateProjectFile();

        var result = helper.Compile();

        // Compilation should fail because ChildB tries to access a protected member
        // of module_a.Base, which it is NOT a subclass of
        Assert.False(result.Success,
            "Should deny protected access between unrelated classes with the same name. " +
            "Got no errors but expected protected access error.");

        var errors = result.Diagnostics.GetErrors().Select(d => d.Message).ToList();

        // Should report a protected access error for a._value
        Assert.Contains(errors,
            e => e.Contains("Cannot access protected member") && e.Contains("_value"));
    }

    [Fact]
    public void CrossModule_SameNameTypes_ProtectedAccessAllowedInActualHierarchy()
    {
        // Verify that protected access IS granted within the actual hierarchy,
        // even when another module has a class with the same name.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("CrossModuleAccessOkTest")
            .AddSourceFile("module_a.spy", @"
class Base:
    _value: int

    def __init__(self):
        self._value = 42
")
            .AddSourceFile("module_b.spy", @"
class Base:
    _data: str

    def __init__(self):
        self._data = ""hello""
")
            .AddSourceFile("main.spy", @"
from module_a import Base as BaseA
from module_b import Base as BaseB

class ChildA(BaseA):
    def get_value(self) -> int:
        return self._value  # OK - actual subclass of module_a.Base

class ChildB(BaseB):
    def get_data(self) -> str:
        return self._data  # OK - actual subclass of module_b.Base

def main():
    a: ChildA = ChildA()
    b: ChildB = ChildB()
    print(a.get_value())
    print(b.get_data())
")
            .CreateProjectFile();

        var result = helper.Compile();

        // Compilation should succeed - each child accesses protected members
        // of its own parent, not the unrelated same-named class
        Assert.True(result.Success,
            "Protected access within actual hierarchy should be allowed. Errors: " +
            string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }
}
