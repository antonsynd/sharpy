using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Comprehensive tests for 'self' handling in semantic analysis.
/// Tests cover:
/// - self parameter typing
/// - self.field access resolution
/// - self.method() call resolution
/// - Validation rules (self outside class, self reassignment)
/// - Access modifier enforcement
/// </summary>
public class SelfHandlingTests
{
    private (Module, SymbolTable, SemanticInfo, TypeChecker) CompileAndCheck(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        // Name resolution first
        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance(); // Second pass: resolve inheritance

        // Type checking
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);

        return (module, symbolTable, semanticInfo, typeChecker);
    }

    #region Positive Tests - Valid self Usage

    [Fact]
    public void SelfParameter_ResolesToClassType()
    {
        var source = @"
class Person:
    name: str

    def get_name(self) -> str:
        return self.name
";
        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have no errors
        typeChecker.Errors.Should().BeEmpty();

        // Verify the class was registered
        var personSymbol = symbolTable.LookupType("Person");
        personSymbol.Should().NotBeNull();
        personSymbol!.Name.Should().Be("Person");
    }

    [Fact]
    public void SelfFieldAccess_ResolvesCorrectly()
    {
        var source = @"
class Point:
    x: int
    y: int

    def magnitude_squared(self) -> int:
        return self.x * self.x + self.y * self.y
";
        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have no errors
        typeChecker.Errors.Should().BeEmpty();

        // Verify field access worked correctly
        var pointSymbol = symbolTable.LookupType("Point");
        pointSymbol.Should().NotBeNull();
        pointSymbol!.Fields.Should().HaveCount(2);
        pointSymbol.Fields[0].Name.Should().Be("x");
        pointSymbol.Fields[1].Name.Should().Be("y");
    }

    [Fact]
    public void SelfMethodCall_ResolvesCorrectly()
    {
        var source = @"
class Calculator:
    def add(self, a: int, b: int) -> int:
        return a + b

    def add_three(self, a: int, b: int, c: int) -> int:
        return self.add(a, b) + c
";
        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have no errors
        typeChecker.Errors.Should().BeEmpty();

        // Verify method call worked
        var calcSymbol = symbolTable.LookupType("Calculator");
        calcSymbol.Should().NotBeNull();
        calcSymbol!.Methods.Should().HaveCount(2);
    }

    [Fact]
    public void SelfInInit_WorksCorrectly()
    {
        var source = @"
class Person:
    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age
";
        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have no errors
        typeChecker.Errors.Should().BeEmpty();

        // Verify __init__ method was registered
        var personSymbol = symbolTable.LookupType("Person");
        personSymbol.Should().NotBeNull();
        var initMethod = personSymbol!.Methods.FirstOrDefault(m => m.Name == "__init__");
        initMethod.Should().NotBeNull();
    }

    [Fact]
    public void SelfInMultipleMethods_WorksCorrectly()
    {
        var source = @"
class Rectangle:
    width: int
    height: int

    def area(self) -> int:
        return self.width * self.height

    def perimeter(self) -> int:
        return 2 * (self.width + self.height)

    def is_square(self) -> bool:
        return self.width == self.height
";
        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have no errors
        typeChecker.Errors.Should().BeEmpty();

        // Verify all methods were registered
        var rectSymbol = symbolTable.LookupType("Rectangle");
        rectSymbol.Should().NotBeNull();
        rectSymbol!.Methods.Should().HaveCount(3);
    }

    [Fact]
    public void SelfWithChainedMethodCalls_WorksCorrectly()
    {
        var source = @"
class Builder:
    value: int

    def set_value(self, v: int):
        self.value = v

    def get_value(self) -> int:
        return self.value

    def build(self) -> int:
        self.set_value(42)
        return self.get_value()
";
        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have no errors
        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Negative Tests - Invalid self Usage

    [Fact]
    public void SelfOutsideClass_ProducesError()
    {
        var source = @"
def greet() -> str:
    return self.name
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should produce error about self outside class
        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e =>
            e.Message.Contains("'self' can only be used inside instance methods"));
    }

    [Fact]
    public void SelfReassignment_ProducesError()
    {
        var source = @"
class Person:
    name: str

    def reset(self):
        self = Person()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should produce error about reassigning self
        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e =>
            e.Message.Contains("Cannot reassign 'self'"));
    }

    [Fact]
    public void SelfAccessNonexistentField_ProducesError()
    {
        var source = @"
class Person:
    name: str

    def greet(self) -> int:
        return self.age
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should produce error about non-existent field
        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e =>
            e.Message.Contains("has no member 'age'"));
    }

    [Fact]
    public void SelfAccessNonexistentMethod_ProducesError()
    {
        var source = @"
class Person:
    name: str

    def greet(self):
        self.say_hello()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should produce error about non-existent method
        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e =>
            e.Message.Contains("has no member 'say_hello'"));
    }

    [Fact]
    public void MethodWithoutSelf_IsTreatedAsStatic()
    {
        // In Sharpy, methods without 'self' as the first parameter are treated as static methods
        var source = @"
class Person:
    def greet(other: int):  # No self - this is a static method
        pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // No error - this is treated as a valid static method
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void MethodWithNoParams_IsTreatedAsStatic()
    {
        // In Sharpy, methods without parameters are treated as static methods
        var source = @"
class Person:
    def greet():  # No parameters - this is a static method
        pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // No error - this is treated as a valid static method
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void SelfInModuleScope_ProducesError()
    {
        var source = @"
x: int = self.value
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should produce error about self outside class
        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e =>
            e.Message.Contains("'self' can only be used inside instance methods"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void SelfFieldAssignment_WorksCorrectly()
    {
        var source = @"
class Counter:
    count: int

    def increment(self):
        self.count = self.count + 1
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have no errors
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void SelfReturnType_WorksCorrectly()
    {
        var source = @"
class Builder:
    value: int

    def set_value(self, v: int):
        self.value = v
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have no errors (void return is fine)
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void SelfInConditional_WorksCorrectly()
    {
        var source = @"
class Person:
    age: int

    def is_adult(self) -> bool:
        if self.age >= 18:
            return True
        else:
            return False
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have no errors
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void SelfInLoop_WorksCorrectly()
    {
        var source = @"
class Counter:
    count: int

    def count_to_ten(self):
        while self.count < 10:
            self.count = self.count + 1
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have no errors
        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Type Safety Tests

    [Fact]
    public void SelfFieldAccess_RespectsTypeAnnotations()
    {
        var source = @"
class Person:
    name: str
    age: int

    def get_info(self) -> str:
        x: int = self.name
        return ""error""
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should produce type error (can't assign str to int)
        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e =>
            e.Message.Contains("Cannot assign"));
    }

    [Fact]
    public void SelfMethodCall_WithCorrectArguments_PassesTypeCheck()
    {
        var source = @"
class Calculator:
    def add(self, a: int, b: int) -> int:
        return a + b

    def test(self) -> int:
        result: int = self.add(5, 10)
        return result
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have no errors when argument types are correct
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void SelfMethodCall_ReturnsCorrectType()
    {
        var source = @"
class Calculator:
    def add(self, a: int, b: int) -> int:
        return a + b

    def test(self) -> int:
        return self.add(1, 2)
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have no errors when return type matches
        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion
}
