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
        var semanticBinding = new SemanticBinding();

        // Name resolution first
        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance(); // Second pass: resolve inheritance
        semanticBinding.MaterializeInheritance();

        // Type checking
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance)
        {
            SemanticBinding = semanticBinding
        };

        return (module, symbolTable, semanticInfo, typeChecker);
    }

    private (Module, SymbolTable, SemanticInfo, TypeChecker, NameResolver) CompileAndCheckWithNameResolver(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        // Name resolution first
        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance(); // Second pass: resolve inheritance
        semanticBinding.MaterializeInheritance();

        // Type checking
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance)
        {
            SemanticBinding = semanticBinding
        };

        return (module, symbolTable, semanticInfo, typeChecker, nameResolver);
    }

    [Fact]
    public void ChecksSimpleVariableDeclaration()
    {
        var source = @"
x: int = 42
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void DetectsTypeErrorInAssignment()
    {
        var source = @"
x: int = 5
y: str = x
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("Cannot assign");
    }

    [Fact]
    public void InfersAutoType()
    {
        // Don't wrap - we need to inspect the AST structure directly
        var source = @"
x: auto = 42
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Filter out module-level errors since we're testing type inference specifically
        var typeErrors = typeChecker.Diagnostics.GetErrors().Where(e => !e.Message.Contains("module level")).ToList();
        typeErrors.Should().BeEmpty();

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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().HaveCount(1);
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("auto");
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("initializer");
    }

    [Fact]
    public void ChecksFunctionReturnType()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void DetectsWrongReturnType()
    {
        var source = @"
def get_name() -> str:
    return 42
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("return");
    }

    [Fact]
    public void ChecksIfConditionIsBoolean()
    {
        var source = @"
def main():
    if True:
        x: int = 1
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void DetectsNonBooleanIfCondition()
    {
        var source = @"
if 42:
    x: int = 1
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("boolean");
    }

    [Fact]
    public void InfersListTypeFromElements()
    {
        // Don't wrap - we need to inspect the AST structure directly
        var source = @"
numbers: auto = [1, 2, 3]
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Filter out module-level errors since we're testing type inference specifically
        var typeErrors = typeChecker.Diagnostics.GetErrors().Where(e => !e.Message.Contains("module level")).ToList();
        typeErrors.Should().BeEmpty();

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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ChecksBinaryOperations()
    {
        var source = @"
x: int = 5 + 3
y: bool = 10 > 5
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ChecksLambdaExpressions()
    {
        // Don't wrap - we need to inspect the AST structure directly
        var source = @"
add: auto = lambda a, b: a + b
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Filter out module-level errors since we're testing type inference specifically
        var typeErrors = typeChecker.Diagnostics.GetErrors().Where(e => !e.Message.Contains("module level")).ToList();
        typeErrors.Should().BeEmpty();

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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().HaveCount(3);
    }

    [Fact]
    public void ChecksConditionalExpression()
    {
        var source = @"
x: int = 5 if True else 10
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ChecksTypeCast()
    {
        var source = @"
x: float = 42 as float
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AllowsNoneForNullableTypes()
    {
        var source = @"
x: int? = None()
y: str? = None()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void RejectsNoneForNonNullableTypes()
    {
        var source = @"
x: int = None
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("None");
    }

    [Fact]
    public void InfersNullableTypeFromNone()
    {
        var source = @"
def main():
    value: str? = None()
    if value is not None:
        x: str = value
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // This test validates that type narrowing is working
        // In the if branch, value should be narrowed from str? to str
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void NarrowedTypeIsStoredInSemanticInfo()
    {
        var source = @"
def main():
    value: str? = None()
    if value is not None:
        x: str = value
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();

        // Find the identifier 'value' in the if block assignment (x: str = value)
        var mainFunc = module.Body.OfType<FunctionDef>().First();
        var ifStmt = mainFunc.Body.OfType<IfStatement>().First();
        var assignment = ifStmt.ThenBody.OfType<VariableDeclaration>().First();
        var valueIdentifier = assignment.InitialValue as Identifier;

        valueIdentifier.Should().NotBeNull();
        valueIdentifier!.Name.Should().Be("value");

        // The narrowed type should be stored in SemanticInfo
        var narrowedType = semanticInfo.GetNarrowedType(valueIdentifier);
        narrowedType.Should().NotBeNull();
        narrowedType.Should().Be(SemanticType.Str); // Narrowed from str? to str
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ChecksPowerOperatorType()
    {
        // Python semantics: power always returns float (double) due to Math.Pow
        // So x ** y returns double, which can be assigned to float (double)
        var source = @"
x: int = 2
y: int = 3
result: float = x ** y
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ChecksBooleanLiterals()
    {
        var source = @"
x: bool = True
y: bool = False
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ChecksMembershipOperator()
    {
        var source = @"
items: list[int] = [1, 2, 3]
result: bool = 2 in items
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ChecksIdentityOperator()
    {
        var source = @"
x: str? = None()
result: bool = x is None
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void TypeNarrowingWithIsInstance()
    {
        var source = @"
class Animal:
    ...

class Dog(Animal):
    ...

def main():
    animal: Animal = Dog()
    if isinstance(animal, Dog):
        result: Dog = animal
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Type narrowing should allow assignment of animal to Dog type
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void TypeNarrowingWithIsInstanceDoesNotAffectElseBranch()
    {
        var source = @"
class Animal:
    ...

class Dog(Animal):
    ...

def main():
    animal: Animal = Dog()
    if isinstance(animal, Dog):
        d: Dog = animal
    else:
        a: Animal = animal
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void TypeNarrowingWithIsInstanceInWhileLoop()
    {
        var source = @"
class Animal:
    ...

class Dog(Animal):
    ...

def main():
    animals: list[Animal] = [Dog(), Dog()]
    i: int = 0
    while i < len(animals) and isinstance(animals[i], Dog):
        dog: Dog = animals[i]
        i = i + 1
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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

def main():
    pet: Animal = Dog()
    if isinstance(pet, Dog):
        d: Dog = pet
    if isinstance(pet, Cat):
        c: Cat = pet
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void CombinedTypeNarrowingIsNotNoneAndIsInstance()
    {
        var source = @"
class Animal:
    ...

class Dog(Animal):
    ...

def main():
    animal: Animal? = Dog()
    if animal is not None and isinstance(animal, Dog):
        d: Dog = animal
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        // Protocol signature validation now happens in SignatureValidator pipeline
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // The error is raised by SignatureValidator during type checking
        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("__init__") && e.Message.Contains("must return"));
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
        // Protocol signature validation now happens in SignatureValidator pipeline
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // The error is raised by SignatureValidator during type checking
        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("__init__"));
    }

    [Fact]
    public void FunctionWithNoneReturnTypeIsVoid()
    {
        var source = @"
def print_message(msg: str) -> None:
    print(msg)

def main():
    print_message('hello')
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void FunctionWithNoneReturnTypeCannotReturnValue()
    {
        var source = @"
def get_value() -> None:
    return 42
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("Cannot return type");
    }

    [Fact]
    public void FunctionWithNoReturnTypeIsEquivalentToNone()
    {
        var source = @"
def do_something():
    print('doing something')

def main():
    do_something()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ValidBitwiseOperations_NoErrors()
    {
        var source = @"
def foo():
    x: int = 3 & 2  # valid bitwise operation
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AllowsLogicalOperationOnNonBool()
    {
        var source = @"
def foo():
    x: bool = 5 and 10  # logical operations on non-bool are allowed (Python semantics)
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Logical operations work on any type in Python (truthy/falsy values)
        // Sharpy follows Python semantics here
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void RejectsInvalidComparisonChainWithMixedTypes()
    {
        var source = @"
def foo():
    x: bool = 1 < 'hello' < 3
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should report an error for comparing int and str
        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("does not support operator '<'");
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Numeric type mixing (int and float) is allowed
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Augmented Assignment Tests

    [Fact]
    public void AugmentedAssignment_IntPlusAssignInt_Succeeds()
    {
        var source = @"
def main():
    x: int = 5
    x += 3
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_IntMinusAssignInt_Succeeds()
    {
        var source = @"
def main():
    x: int = 10
    x -= 3
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_IntStarAssignInt_Succeeds()
    {
        var source = @"
def main():
    x: int = 5
    x *= 2
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_IntSlashAssignInt_ProducesTypeError()
    {
        // Python semantics: /= always returns float (double), so assigning to int should fail
        // Use //= (floor divide) for integer division
        var source = @"
x: int = 10
x /= 2
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e =>
            e.Message.Contains("double") && e.Message.Contains("int"));
    }

    [Fact]
    public void AugmentedAssignment_IntFloorDivAssignInt_Succeeds()
    {
        var source = @"
def main():
    x: int = 10
    x //= 3
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_IntModuloAssignInt_Succeeds()
    {
        var source = @"
def main():
    x: int = 10
    x %= 3
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_IntPowerAssignInt_Succeeds()
    {
        // int ** int now returns int (integer exponentiation), so assigning to int should succeed
        var source = @"
def main():
    x: int = 2
    x **= 3
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_IntBitwiseAndAssignInt_Succeeds()
    {
        var source = @"
def main():
    x: int = 15
    x &= 7
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_IntBitwiseOrAssignInt_Succeeds()
    {
        var source = @"
def main():
    x: int = 8
    x |= 1
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_IntBitwiseXorAssignInt_Succeeds()
    {
        var source = @"
def main():
    x: int = 5
    x ^= 3
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_IntLeftShiftAssignInt_Succeeds()
    {
        var source = @"
def main():
    x: int = 4
    x <<= 2
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_IntRightShiftAssignInt_Succeeds()
    {
        var source = @"
def main():
    x: int = 16
    x >>= 2
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_StrPlusAssignStr_Succeeds()
    {
        var source = @"
def main():
    s: str = ""hello""
    s += "" world""
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_DoublePlusAssignInt_Succeeds()
    {
        var source = @"
def main():
    x: double = 1.5
    x += 2
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_UnsupportedOperatorOnString_ReportsError()
    {
        var source = @"
def main():
    s: str = ""hello""
    s -= "" world""
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        // Check for either legacy or pipeline error format
        var errorMsg = typeChecker.Diagnostics.GetErrors()[0].Message;
        (errorMsg.Contains("does not support") || errorMsg.Contains("Unsupported operand types"))
            .Should().BeTrue($"Expected operator error message but got: {errorMsg}");
    }

    [Fact]
    public void AugmentedAssignment_BitwiseOnDouble_ReportsError()
    {
        var source = @"
def main():
    x: double = 1.5
    x &= 2
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        // Check for either legacy or pipeline error format
        var errorMsg = typeChecker.Diagnostics.GetErrors()[0].Message;
        (errorMsg.Contains("does not support") || errorMsg.Contains("Unsupported operand types"))
            .Should().BeTrue($"Expected operator error message but got: {errorMsg}");
    }

    [Fact]
    public void AugmentedAssignment_InFunction_Succeeds()
    {
        var source = @"
def increment(x: int) -> int:
    x += 1
    return x
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_InLoop_Succeeds()
    {
        var source = @"
def main():
    total: int = 0
    items: list[int] = [1, 2, 3]
    for i in items:
        total += i
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_WithExpression_Succeeds()
    {
        var source = @"
def main():
    x: int = 5
    y: int = 3
    x += y * 2
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_ToConstant_ReportsError()
    {
        var source = @"
const X: int = 10
X += 5
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("Cannot");
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("constant");
    }

    #endregion

    #region Keyword Argument Tests

    [Fact]
    public void KeywordArgument_ValidUsage_NoError()
    {
        var source = @"
def greet(name: str, greeting: str = 'Hello') -> str:
    return greeting + ', ' + name

result: str = greet('World', greeting='Hi')
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void KeywordArgument_UnknownParameter_ReportsError()
    {
        var source = @"
def greet(name: str) -> str:
    return 'Hello, ' + name

greet(unknown='test')
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("Unknown keyword argument 'unknown'");
    }

    [Fact]
    public void KeywordArgument_DuplicatePositionalAndKeyword_ReportsError()
    {
        var source = @"
def greet(name: str, greeting: str = 'Hello') -> str:
    return greeting + ', ' + name

greet('World', name='Alice')
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("Argument 'name' was already provided positionally");
    }

    [Fact]
    public void KeywordArgument_TypeMismatch_ReportsError()
    {
        var source = @"
def greet(name: str, count: int = 1) -> str:
    return name

greet('World', count='not an int')
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("Cannot pass argument of type");
    }

    #endregion

    #region Constructor Overloading Tests

    [Fact]
    public void DuplicateConstructorSignature_ProducesError()
    {
        var source = @"
class Person:
    name: str

    def __init__(self, name: str):
        self.name = name

    def __init__(self, name: str):
        self.name = name
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("Duplicate constructor signature");
    }

    [Fact]
    public void MultipleConstructors_DifferentSignatures_NoError()
    {
        var source = @"
class Person:
    name: str
    age: int

    def __init__(self):
        self.name = ''
        self.age = 0

    def __init__(self, name: str):
        self.name = name
        self.age = 0

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void MultipleConstructors_DifferentParamTypes_NoError()
    {
        var source = @"
class Value:
    data: object

    def __init__(self, value: int):
        self.data = value

    def __init__(self, value: str):
        self.data = value
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void DuplicateConstructor_SameParamTypes_ProducesError()
    {
        var source = @"
class Box:
    width: int
    height: int

    def __init__(self, w: int, h: int):
        self.width = w
        self.height = h

    def __init__(self, width: int, height: int):
        self.width = width
        self.height = height
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("Duplicate constructor signature");
    }

    [Fact]
    public void SingleConstructor_NoValidationError()
    {
        var source = @"
class Person:
    name: str

    def __init__(self, name: str):
        self.name = name
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ParameterlessConstructor_Works()
    {
        var source = @"
class EmptyClass:
    def __init__(self):
        pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region MaxErrors Truncation

    [Fact]
    public void CheckModule_MaxErrorsReached_EmitsTruncationWarning()
    {
        // Source with many type errors (each line is an independent error)
        var source = @"
def main() -> None:
    x: int = ""a""
    y: int = ""b""
    z: int = ""c""
    w: int = ""d""
    v: int = ""e""
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.MaxErrors = 3;
        typeChecker.CheckModule(module, isEntryPoint: true);

        var errors = typeChecker.Diagnostics.GetErrors().ToList();
        var warnings = typeChecker.Diagnostics.GetWarnings().ToList();

        errors.Should().HaveCount(3, "only MaxErrors errors should be recorded");
        warnings.Where(w => w.Code == "SHP0905").Should().HaveCount(1,
            "exactly one truncation warning should be emitted when MaxErrors is reached");
    }

    [Fact]
    public void CheckModule_BelowMaxErrors_NoTruncationWarning()
    {
        var source = @"
def main() -> None:
    x: int = ""a""
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.MaxErrors = 100;
        typeChecker.CheckModule(module, isEntryPoint: true);

        var warnings = typeChecker.Diagnostics.GetWarnings().ToList();
        warnings.Should().NotContain(w => w.Code == "SHP0905",
            "no truncation warning should appear when under the limit");
    }

    #endregion

    #region Type Narrowing Persistence (Phase 6.2)

    [Fact]
    public void GetEffectiveType_ReturnsNarrowedType_WhenNarrowed()
    {
        var source = @"
def main():
    value: str? = None()
    if value is not None:
        x: str = value
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();

        // Find the identifier 'value' in the if block assignment
        var mainFunc = module.Body.OfType<FunctionDef>().First();
        var ifStmt = mainFunc.Body.OfType<IfStatement>().First();
        var assignment = ifStmt.ThenBody.OfType<VariableDeclaration>().First();
        var valueIdentifier = assignment.InitialValue as Identifier;

        valueIdentifier.Should().NotBeNull();

        // GetEffectiveType should return the narrowed type (str), not the original (str?)
        var effectiveType = semanticInfo.GetEffectiveType(valueIdentifier!);
        effectiveType.Should().Be(SemanticType.Str);
    }

    [Fact]
    public void GetEffectiveType_ReturnsExpressionType_WhenNotNarrowed()
    {
        var source = @"
def main():
    value: str = ""hello""
    x: str = value
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();

        // Find the identifier 'value' in the assignment (x: str = value)
        var mainFunc = module.Body.OfType<FunctionDef>().First();
        var assignment = mainFunc.Body.OfType<VariableDeclaration>().Last();
        var valueIdentifier = assignment.InitialValue as Identifier;

        valueIdentifier.Should().NotBeNull();

        // GetEffectiveType should return expression type since there's no narrowing
        var effectiveType = semanticInfo.GetEffectiveType(valueIdentifier!);
        effectiveType.Should().Be(SemanticType.Str);
    }

    [Fact]
    public void TypeNarrowing_InWhileLoop_PersistsCorrectly()
    {
        var source = @"
def main():
    value: int? = None()
    while value is not None:
        x: int = value
        break
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();

        // Find the identifier 'value' in the while body
        var mainFunc = module.Body.OfType<FunctionDef>().First();
        var whileStmt = mainFunc.Body.OfType<WhileStatement>().First();
        var assignment = whileStmt.Body.OfType<VariableDeclaration>().First();
        var valueIdentifier = assignment.InitialValue as Identifier;

        valueIdentifier.Should().NotBeNull();

        // Narrowed type should be persisted
        var narrowedType = semanticInfo.GetNarrowedType(valueIdentifier!);
        narrowedType.Should().NotBeNull();
        narrowedType.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void TypeNarrowing_NestedIfStatements_NarrowsCorrectly()
    {
        var source = @"
def main():
    a: int? = None()
    b: str? = None()
    if a is not None:
        if b is not None:
            x: int = a
            y: str = b
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();

        // Both variables should be narrowed within the nested if
        var mainFunc = module.Body.OfType<FunctionDef>().First();
        var outerIf = mainFunc.Body.OfType<IfStatement>().First();
        var innerIf = outerIf.ThenBody.OfType<IfStatement>().First();

        var xAssignment = innerIf.ThenBody.OfType<VariableDeclaration>().First();
        var aIdentifier = xAssignment.InitialValue as Identifier;
        aIdentifier.Should().NotBeNull();

        var yAssignment = innerIf.ThenBody.OfType<VariableDeclaration>().Last();
        var bIdentifier = yAssignment.InitialValue as Identifier;
        bIdentifier.Should().NotBeNull();

        semanticInfo.GetEffectiveType(aIdentifier!).Should().Be(SemanticType.Int);
        semanticInfo.GetEffectiveType(bIdentifier!).Should().Be(SemanticType.Str);
    }

    [Fact]
    public void TypeNarrowing_DoesNotPersist_OutsideScope()
    {
        // After the if block, the variable should have its original type, not narrowed
        var source = @"
def main():
    value: str? = None()
    if value is not None:
        x: str = value  # narrowed here
    y: str? = value  # not narrowed here - should be str?
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();

        // Find the last assignment (y: str? = value)
        var mainFunc = module.Body.OfType<FunctionDef>().First();
        var yAssignment = mainFunc.Body.OfType<VariableDeclaration>().Last();
        var valueIdentifier = yAssignment.InitialValue as Identifier;

        valueIdentifier.Should().NotBeNull();

        // Outside the if block, there should be no narrowing
        var narrowedType = semanticInfo.GetNarrowedType(valueIdentifier!);
        narrowedType.Should().BeNull("narrowing should not persist outside the if block");
    }

    [Fact]
    public void TypeNarrowing_IsInstance_PersistedToSemanticInfo()
    {
        // Verify isinstance narrowing is persisted via GetEffectiveType
        var source = @"
class Animal:
    ...

class Dog(Animal):
    ...

def main():
    animal: Animal = Dog()
    if isinstance(animal, Dog):
        x: Dog = animal
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();

        // Find the identifier 'animal' in the if block assignment (x: Dog = animal)
        var mainFunc = module.Body.OfType<FunctionDef>().First();
        var ifStmt = mainFunc.Body.OfType<IfStatement>().First();
        var assignment = ifStmt.ThenBody.OfType<VariableDeclaration>().First();
        var animalIdentifier = assignment.InitialValue as Identifier;

        animalIdentifier.Should().NotBeNull();
        animalIdentifier!.Name.Should().Be("animal");

        // The narrowed type should be persisted in SemanticInfo
        var narrowedType = semanticInfo.GetNarrowedType(animalIdentifier);
        narrowedType.Should().NotBeNull("isinstance narrowing should be persisted");
        narrowedType.Should().BeOfType<UserDefinedType>();
        (narrowedType as UserDefinedType)!.Name.Should().Be("Dog");

        // GetEffectiveType should also return the narrowed type
        var effectiveType = semanticInfo.GetEffectiveType(animalIdentifier);
        effectiveType.Should().NotBeNull();
        effectiveType.Should().BeOfType<UserDefinedType>();
        (effectiveType as UserDefinedType)!.Name.Should().Be("Dog");
    }

    [Fact]
    public void GetEffectiveType_ReturnsNull_WhenNoTypeRecorded()
    {
        // Create a fresh SemanticInfo and check GetEffectiveType behavior
        var semanticInfo = new SemanticInfo();
        var dummyExpr = new Identifier { Name = "x" };

        // When no type is recorded, GetEffectiveType should return null
        var effectiveType = semanticInfo.GetEffectiveType(dummyExpr);
        effectiveType.Should().BeNull();
    }

    [Fact]
    public void TypeNarrowing_FunctionsHaveIsolatedNarrowingContext()
    {
        // Each function should have its own isolated narrowing context.
        // This test verifies that TypeNarrowingContext.EnterIsolatedScope()
        // is properly isolating function bodies.
        //
        // We test this by checking that a variable narrowed in one function
        // is NOT visible as narrowed when we process a subsequent function.
        // Since they're separate functions, the narrowing state should reset.
        var source = @"
def first():
    x: int? = 42
    if x is not None:
        y: int = x  # x is narrowed inside if block

def second():
    z: int? = None()
    # z should not be narrowed here
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should compile without errors
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();

        // The key point is that type checking both functions works correctly
        // and the narrowing from first() doesn't affect second()
    }

    [Fact]
    public void TypeNarrowing_DoesNotLeakBetweenIndependentFunctions()
    {
        // Verify that narrowing in one function doesn't affect another
        var source = @"
def first():
    x: int? = 42
    if x is not None:
        y: int = x  # x narrowed here

def second():
    x: int? = None()
    # x should be int? here, not narrowed
    z: int? = x
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void TypeNarrowing_IsolatedScopeContext_VerifyEnterIsolatedScopeUsed()
    {
        // Verify that the isolated scope is used for function definitions.
        // This test verifies the infrastructure is correctly wired up by checking
        // that narrowing inside one function body doesn't leak to subsequent code.
        //
        // The TypeNarrowingContext.EnterIsolatedScope() is called in CheckFunction
        // (TypeChecker.Definitions.cs:49) to ensure narrowings don't cross function boundaries.
        var source = @"
def first():
    x: int? = 42
    if x is not None:
        y: int = x  # x is narrowed inside if block

def second():
    z: int? = 42
    if z is not None:
        w: int = z  # z is narrowed independently
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Both functions should type-check successfully with independent narrowing contexts
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void TypeNarrowing_FunctionBoundaryIsolation_EnsuresCorrectSemantics()
    {
        // Verify that narrowing from the condition in one function
        // does not affect the type seen in subsequent functions.
        // This is critical for correctness: each function gets a fresh narrowing context.
        var source = @"
def check_and_use() -> int:
    value: int? = 42
    if value is not None:
        return value  # value is narrowed to int here
    return 0

def another_check() -> int:
    data: int? = None()
    if data is not None:
        return data  # data is narrowed to int here
    return -1
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void TypeNarrowing_SiblingFunctions_HaveIndependentNarrowingContexts()
    {
        // Test that each top-level function has its own isolated narrowing context.
        // This verifies that EnterIsolatedScope() is called correctly for each function.
        //
        // This is the key behavior from task 1.7: functions should not inherit narrowing
        // from other scopes. While nested functions (local functions) aren't fully supported
        // in Sharpy yet, the same isolation mechanism applies to sibling functions.
        var source = @"
def func_with_narrowing() -> int:
    x: int? = 42
    if x is not None:
        return x  # x is narrowed to int here
    return 0

def func_without_narrowing() -> int?:
    # This function should have its own isolated narrowing context
    # Narrowing from func_with_narrowing should NOT leak here
    y: int? = None()
    return y  # y should be int?, not narrowed
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Both functions should type-check successfully with independent narrowing contexts
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();

        // Find return statement in func_without_narrowing and verify y is not narrowed
        var secondFunc = module.Body.OfType<FunctionDef>().Skip(1).First();
        var returnStmt = secondFunc.Body.OfType<ReturnStatement>().First();
        var yIdentifier = returnStmt.Value as Identifier;

        yIdentifier.Should().NotBeNull();
        yIdentifier!.Name.Should().Be("y");

        // y should NOT be narrowed - each function has isolated narrowing context
        var narrowedType = semanticInfo.GetNarrowedType(yIdentifier);
        narrowedType.Should().BeNull("narrowing should not leak between functions");
    }

    [Fact]
    public void TypeNarrowing_FunctionAfterNarrowedBlock_HasFreshContext()
    {
        // Verify that a function defined after code with narrowing has a fresh context.
        // Note: This tests the isolation mechanism at the function boundary level.
        var source = @"
def first() -> int:
    value: int? = 42
    if value is not None:
        return value  # value is narrowed to int
    return 0

def second() -> int:
    # Even if first() was just processed, second() has its own isolated context
    data: int? = 42
    if data is not None:
        return data  # data narrowed independently
    return -1
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Both functions should compile without errors
        // This verifies that EnterIsolatedScope() creates a clean narrowing state
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void TypeNarrowing_MultipleFunctionsWithSameVariableName_IndependentNarrowing()
    {
        // Test that multiple functions using the same variable name have independent
        // narrowing contexts. This is critical for the isolation mechanism.
        var source = @"
def check_a() -> int:
    x: int? = 42
    if x is not None:
        return x  # x is narrowed to int

    return 0

def check_b() -> int?:
    x: int? = None()
    # x should be int? here, NOT narrowed from check_a()
    return x

def check_c() -> int:
    x: int? = 100
    if x is not None:
        # x narrowed independently in check_c()
        return x
    return 0
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();

        // Find x in check_b's return statement - should NOT be narrowed
        var checkB = module.Body.OfType<FunctionDef>().Skip(1).First();
        checkB.Name.Should().Be("check_b");
        var returnStmt = checkB.Body.OfType<ReturnStatement>().First();
        var xIdentifier = returnStmt.Value as Identifier;

        xIdentifier.Should().NotBeNull();
        var narrowedType = semanticInfo.GetNarrowedType(xIdentifier!);
        narrowedType.Should().BeNull("x in check_b should not be affected by narrowing in check_a");
    }

    [Fact]
    public void TypeNarrowing_NestedFunctionDefinition_DoesNotInheritNarrowing()
    {
        // Task 1.7: Critical edge case - nested function narrowing isolation
        //
        // When a function is defined inside another function's control-flow narrowing block,
        // the nested function should NOT inherit the narrowing. This is because:
        // 1. Nested functions can be called later when the narrowing condition no longer holds
        // 2. Narrowing is control-flow based, not lexical
        //
        // Example: If x is narrowed to int after `if x is not None:`, a nested function
        // defined in that block should still see x as int? (the original type).
        //
        // Note: While Sharpy doesn't yet fully support calling nested functions,
        // the narrowing isolation mechanism must still be correct for when they are.
        var source = @"
def main():
    x: int? = 42
    if x is not None:
        # x is narrowed to int here in the outer function
        y: int = x  # This works - x is narrowed

        def inner() -> int?:
            # x should NOT be narrowed here - inner() has its own scope
            # The nested function might be called later when x could be None
            return x  # x should be int?, not int
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should compile without errors
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();

        // Find the nested function 'inner' and its return statement
        var mainFunc = module.Body.OfType<FunctionDef>().First();
        mainFunc.Name.Should().Be("main");

        var ifStmt = mainFunc.Body.OfType<IfStatement>().First();
        var innerFunc = ifStmt.ThenBody.OfType<FunctionDef>().First();
        innerFunc.Name.Should().Be("inner");

        var returnStmt = innerFunc.Body.OfType<ReturnStatement>().First();
        var xIdentifier = returnStmt.Value as Identifier;

        xIdentifier.Should().NotBeNull();
        xIdentifier!.Name.Should().Be("x");

        // x inside inner() should NOT be narrowed - it should be the original int? type
        var narrowedType = semanticInfo.GetNarrowedType(xIdentifier);
        narrowedType.Should().BeNull("narrowing should not cross function boundaries into nested functions");
    }

    [Fact]
    public void TypeNarrowing_NestedFunctionInWhileLoop_DoesNotInheritNarrowing()
    {
        // Verify that nested functions in while loops also have isolated narrowing
        var source = @"
def process():
    value: int? = 42
    while value is not None:
        # value is narrowed to int here
        temp: int = value  # This works

        def helper() -> int?:
            # value should NOT be narrowed here
            return value  # value should be int?

        break
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();

        // Find nested function and verify narrowing is isolated
        var processFunc = module.Body.OfType<FunctionDef>().First();
        var whileStmt = processFunc.Body.OfType<WhileStatement>().First();
        var helperFunc = whileStmt.Body.OfType<FunctionDef>().First();

        var returnStmt = helperFunc.Body.OfType<ReturnStatement>().First();
        var valueIdentifier = returnStmt.Value as Identifier;

        valueIdentifier.Should().NotBeNull();
        var narrowedType = semanticInfo.GetNarrowedType(valueIdentifier!);
        narrowedType.Should().BeNull("narrowing should not cross function boundaries");
    }

    [Fact]
    public void TypeNarrowing_DeeplyNestedFunctions_EachHasIsolatedContext()
    {
        // Verify that deeply nested functions each have their own isolated narrowing
        var source = @"
def outer():
    a: int? = 1
    if a is not None:
        # a narrowed to int
        x: int = a

        def middle():
            b: int? = 2
            if b is not None:
                # b narrowed to int, but a is NOT narrowed here
                y: int = b

                def inner():
                    c: int? = 3
                    if c is not None:
                        # c narrowed, but a and b are NOT narrowed here
                        z: int = c
                        # a and b should be their original optional types
                        result_a: int? = a  # a is int? here
                        result_b: int? = b  # b is int? here
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should compile without errors - all types are correctly isolated
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void TypeNarrowing_LambdaDoesNotInheritNarrowing()
    {
        // Test that lambdas have isolated narrowing scopes, similar to nested functions.
        // A lambda defined inside a narrowing block could be called later when the
        // narrowing condition no longer holds.
        //
        // This test verifies that using an optional variable inside a lambda (which is
        // narrowed in the enclosing scope) produces an error because the lambda doesn't
        // inherit the narrowing.
        var source = @"
def process() -> int:
    value: int? = 42
    if value is not None:
        # value is narrowed to int here in the outer scope
        y: int = value  # This works - value is narrowed

        # Lambda should see value as int? (not narrowed)
        # Arithmetic on int? should produce an error
        f = lambda: value + 1
    return 0
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have an error because int? doesn't support +
        var errors = typeChecker.Diagnostics.GetErrors().ToList();
        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("does not support operator '+'");
    }

    [Fact]
    public void TypeNarrowing_LambdaInWhileLoop_DoesNotInheritNarrowing()
    {
        // Verify that lambdas in while loops also have isolated narrowing scopes
        var source = @"
def process() -> int:
    value: int? = 42
    while value is not None:
        # value is narrowed to int here
        temp: int = value  # This works in outer scope

        # Lambda should NOT inherit narrowing
        f = lambda: value + 1  # Error: int? doesn't support +
        break
    return 0
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have an error because int? doesn't support +
        var errors = typeChecker.Diagnostics.GetErrors().ToList();
        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("does not support operator '+'");
    }

    #endregion
}
