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

    [Fact]
    public void ConstructorWithNoReturnTypeIsValid()
    {
        var source = @"
class Person:
    name: str

    def __init__(self, name: str):
        self.name = name
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ConstructorWithNoneReturnTypeIsValid()
    {
        var source = @"
class Person:
    name: str

    def __init__(self, name: str) -> None:
        self.name = name
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ConstructorWithInvalidReturnTypeRaisesError()
    {
        var source = @"
class Person:
    name: str

    def __init__(self, name: str) -> int:
        self.name = name
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors[0].Message.Should().Contain("__init__");
        typeChecker.Errors[0].Message.Should().Contain("cannot have return type");
    }

    [Fact]
    public void ConstructorWithStrReturnTypeRaisesError()
    {
        var source = @"
class Person:
    name: str

    def __init__(self, name: str) -> str:
        self.name = name
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors[0].Message.Should().Contain("__init__");
    }

    [Fact]
    public void FunctionWithNoneReturnTypeIsVoid()
    {
        var source = @"
def print_message(msg: str) -> None:
    print(msg)

print_message('hello')
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void FunctionWithNoneReturnTypeCannotReturnValue()
    {
        var source = @"
def get_value() -> None:
    return 42
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors[0].Message.Should().Contain("Cannot return type");
    }

    [Fact]
    public void FunctionWithNoReturnTypeIsEquivalentToNone()
    {
        var source = @"
def do_something():
    print('doing something')

do_something()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void FunctionWithNoneReturnTypeCanHaveEmptyReturn()
    {
        var source = @"
def maybe_print(condition: bool, msg: str) -> None:
    if condition:
        print(msg)
        return
    print('default')
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    #region Operator Type Validation Tests

    [Fact]
    public void AllowsValidNumericAddition()
    {
        var source = @"
def foo():
    x: int = 5 + 10
    y: double = 3.14 + 2.71
    z: int = 5 + 10 + 15
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsValidStringConcatenation()
    {
        var source = @"
def foo():
    x: str = 'hello' + 'world'
    y: str = 'a' + 'b' + 'c'
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsValidNumericComparison()
    {
        var source = @"
def foo():
    a: bool = 5 < 10
    b: bool = 3.14 >= 2.71
    c: bool = 100 == 100
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsValidStringComparison()
    {
        var source = @"
def foo():
    a: bool = 'apple' < 'banana'
    b: bool = 'hello' == 'hello'
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsValidUnaryMinus()
    {
        var source = @"
def foo():
    x: int = -5
    y: double = -3.14
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsBitwiseOperationsOnIntegers()
    {
        var source = @"
def foo():
    x: int = 5 & 3
    y: int = 10 | 2
    z: int = 7 ^ 4
    w: int = ~5
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidBitwiseOperations_NoErrors()
    {
        var source = @"
def foo():
    x: int = 3 & 2  # valid bitwise operation
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsLogicalOperationOnNonBool()
    {
        var source = @"
def foo():
    x: bool = 5 and 10  # logical operations on non-bool are allowed (Python semantics)
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Logical operations work on any type in Python (truthy/falsy values)
        // Sharpy follows Python semantics here
        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Comparison Chain Validation Tests

    [Fact]
    public void AllowsValidNumericComparisonChain()
    {
        var source = @"
def foo():
    x: bool = 1 < 2 < 3
    y: bool = 0 <= 5 <= 10
    z: bool = 10 > 5 > 0
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsValidMixedComparisonChain()
    {
        var source = @"
def foo():
    x: bool = 1 < 2 <= 3 < 4
    y: bool = 10 >= 5 > 0
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsValidEqualityComparisonChain()
    {
        var source = @"
def foo():
    a: int = 5
    b: int = 5
    c: int = 5
    x: bool = a == b == c
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsValidStringComparisonChain()
    {
        var source = @"
def foo():
    x: bool = 'a' < 'b' < 'c'
    y: bool = 'apple' <= 'banana' <= 'cherry'
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void RejectsInvalidComparisonChainWithMixedTypes()
    {
        var source = @"
def foo():
    x: bool = 1 < 'hello' < 3
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Should report an error for comparing int and str
        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors[0].Message.Should().Contain("does not support operator '<'");
    }

    [Fact]
    public void AllowsComparisonChainWithFloatAndInt()
    {
        var source = @"
def foo():
    x: bool = 1 < 2.5 < 4
    y: bool = 0.5 <= 1 <= 1.5
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Numeric type mixing (int and float) is allowed
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ComparisonChainWithVariables()
    {
        var source = @"
def foo():
    a: int = 1
    b: int = 2
    c: int = 3
    x: bool = a < b < c
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ComparisonChainInIfCondition()
    {
        var source = @"
def foo(x: int):
    if 0 < x < 100:
        print('x is in range')
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ComparisonChainInWhileCondition()
    {
        var source = @"
def foo():
    x: int = 50
    while 0 < x < 100:
        x = x - 1
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion
}
