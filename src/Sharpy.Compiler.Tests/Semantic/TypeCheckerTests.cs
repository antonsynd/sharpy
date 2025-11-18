using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using SemFunctionType = Sharpy.Compiler.Semantic.FunctionType;

namespace Sharpy.Compiler.Tests.Semantic;

public class TypeCheckerTests
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

    [Fact]
    public void ChecksSimpleVariableDeclaration()
    {
        var source = @"
x: int = 42
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DetectsTypeErrorInAssignment()
    {
        var source = @"
x: int = 5
y: str = x
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors[0].Message.Should().Contain("Cannot assign");
    }

    [Fact]
    public void InfersAutoType()
    {
        var source = @"
x: auto = 42
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();

        // Verify type was inferred
        var varDecl = (VariableDeclaration)module.Body[0];
        var inferredType = semanticInfo.GetTypeAnnotation(varDecl.Type);
        inferredType.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ReportsErrorForAutoWithoutInitializer()
    {
        var source = @"
x: auto
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().HaveCount(1);
        typeChecker.Errors[0].Message.Should().Contain("auto");
        typeChecker.Errors[0].Message.Should().Contain("initializer");
    }

    [Fact]
    public void ChecksFunctionReturnType()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DetectsWrongReturnType()
    {
        var source = @"
def get_name() -> str:
    return 42
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors[0].Message.Should().Contain("return");
    }

    [Fact]
    public void ChecksIfConditionIsBoolean()
    {
        var source = @"
if True:
    x: int = 1
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DetectsNonBooleanIfCondition()
    {
        var source = @"
if 42:
    x: int = 1
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors[0].Message.Should().Contain("boolean");
    }

    [Fact]
    public void InfersListTypeFromElements()
    {
        var source = @"
numbers: auto = [1, 2, 3]
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();

        var varDecl = (VariableDeclaration)module.Body[0];
        var inferredType = semanticInfo.GetTypeAnnotation(varDecl.Type);
        inferredType.Should().BeOfType<GenericType>();
        var genericType = (GenericType)inferredType;
        genericType.Name.Should().Be("list");
        genericType.TypeArguments.Should().HaveCount(1);
        genericType.TypeArguments[0].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ChecksClassMethods()
    {
        var source = @"
class Person:
    name: str

    def __init__(self, name: str):
        self.name = name

    def greet(self) -> str:
        return self.name
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ChecksBinaryOperations()
    {
        var source = @"
x: int = 5 + 3
y: bool = 10 > 5
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ChecksLambdaExpressions()
    {
        var source = @"
add: auto = lambda a, b: a + b
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();

        var varDecl = (VariableDeclaration)module.Body[0];
        var inferredType = semanticInfo.GetTypeAnnotation(varDecl.Type!);
        inferredType.Should().BeOfType<SemFunctionType>();
    }

    [Fact]
    public void HandlesMultipleErrorsGracefully()
    {
        var source = @"
x: int = ""hello""
y: str = 42
z: bool = 3.14
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().HaveCount(3);
    }

    [Fact]
    public void ChecksConditionalExpression()
    {
        var source = @"
x: int = 5 if True else 10
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ChecksTypeCast()
    {
        var source = @"
x: float = 42 as float
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsNoneForNullableTypes()
    {
        var source = @"
x: int? = None
y: str? = None
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void RejectsNoneForNonNullableTypes()
    {
        var source = @"
x: int = None
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors[0].Message.Should().Contain("None");
    }

    [Fact]
    public void InfersNullableTypeFromNone()
    {
        var source = @"
value: str? = None
if value is not None:
    x: str = value
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // This test validates that type narrowing is working
        // In the if branch, value should be narrowed from str? to str
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ChecksDivisionProducesDouble()
    {
        var source = @"
x: int = 10
y: int = 3
result: double = x / y
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ChecksFloorDivisionProducesInt()
    {
        var source = @"
x: int = 10
y: int = 3
result: int = x // y
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ChecksPowerOperatorType()
    {
        var source = @"
x: int = 2
y: int = 3
result: int = x ** y
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ChecksBooleanLiterals()
    {
        var source = @"
x: bool = True
y: bool = False
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ChecksLogicalOperators()
    {
        var source = @"
x: bool = True and False
y: bool = True or False
z: bool = not True
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ChecksMembershipOperator()
    {
        var source = @"
items: list[int] = [1, 2, 3]
result: bool = 2 in items
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ChecksIdentityOperator()
    {
        var source = @"
x: str? = None
result: bool = x is None
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void TypeNarrowingWithIsInstance()
    {
        var source = @"
class Animal:
    ...

class Dog(Animal):
    ...

animal: Animal = Dog()
if isinstance(animal, Dog):
    result: Dog = animal
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Type narrowing should allow assignment of animal to Dog type
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void TypeNarrowingWithIsInstanceDoesNotAffectElseBranch()
    {
        var source = @"
class Animal:
    ...

class Dog(Animal):
    ...

animal: Animal = Dog()
if isinstance(animal, Dog):
    d: Dog = animal
else:
    a: Animal = animal
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void TypeNarrowingWithIsInstanceInWhileLoop()
    {
        var source = @"
class Animal:
    ...

class Dog(Animal):
    ...

animals: list[Animal] = [Dog(), Dog()]
i: int = 0
while i < len(animals) and isinstance(animals[i], Dog):
    dog: Dog = animals[i]
    i = i + 1
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void IsInstanceWithMultipleTypeChecks()
    {
        var source = @"
class Animal:
    ...

class Dog(Animal):
    ...

class Cat(Animal):
    ...

pet: Animal = Dog()
if isinstance(pet, Dog):
    d: Dog = pet
if isinstance(pet, Cat):
    c: Cat = pet
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void CombinedTypeNarrowingIsNotNoneAndIsInstance()
    {
        var source = @"
class Animal:
    ...

class Dog(Animal):
    ...

animal: Animal? = Dog()
if animal is not None and isinstance(animal, Dog):
    d: Dog = animal
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }
}
