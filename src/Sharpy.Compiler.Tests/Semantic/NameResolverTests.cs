using Xunit;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Semantic;

public class NameResolverTests
{
    private (NameResolver resolver, Module module, SymbolTable symbolTable) CreateResolver(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var resolver = new NameResolver(symbolTable);

        return (resolver, module, symbolTable);
    }

    [Fact]
    public void TestSimpleClassDeclaration()
    {
        var source = @"
class Person:
    name: str
    age: int
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        // Verify class symbol was created
        var personType = symbolTable.LookupType("Person");
        Assert.NotNull(personType);
        Assert.Equal("Person", personType.Name);
        Assert.Equal(TypeKind.Class, personType.TypeKind);

        // Verify fields were added
        Assert.Equal(2, personType.Fields.Count);
        Assert.Contains(personType.Fields, f => f.Name == "name");
        Assert.Contains(personType.Fields, f => f.Name == "age");
    }

    [Fact]
    public void TestDuplicateClassDefinition()
    {
        var source = @"
class Person:
    pass

class Person:
    pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Single(resolver.Errors);
        Assert.Contains("already defined", resolver.Errors[0].Message);
    }

    [Fact]
    public void TestAccessLevelDetection()
    {
        var source = @"
class MyClass:
    public_field: int
    _protected_field: int
    __private_field: int
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var classType = symbolTable.LookupType("MyClass");
        Assert.NotNull(classType);

        var publicField = classType.Fields.First(f => f.Name == "public_field");
        var protectedField = classType.Fields.First(f => f.Name == "_protected_field");
        var privateField = classType.Fields.First(f => f.Name == "__private_field");

        Assert.Equal(AccessLevel.Public, publicField.AccessLevel);
        Assert.Equal(AccessLevel.Protected, protectedField.AccessLevel);
        Assert.Equal(AccessLevel.Private, privateField.AccessLevel);
    }

    [Fact]
    public void TestFunctionDeclaration()
    {
        var source = @"
def greet(name: str) -> str:
    return 'Hello, ' + name
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var greetFunc = symbolTable.LookupFunction("greet");
        Assert.NotNull(greetFunc);
        Assert.Equal("greet", greetFunc.Name);
    }

    [Fact]
    public void TestStructDeclaration()
    {
        var source = @"
struct Point:
    x: float
    y: float
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var pointType = symbolTable.LookupType("Point");
        Assert.NotNull(pointType);
        Assert.Equal(TypeKind.Struct, pointType.TypeKind);
        Assert.Equal(2, pointType.Fields.Count);
    }

    [Fact]
    public void TestInterfaceDeclaration()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> int:
        pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var drawableType = symbolTable.LookupType("IDrawable");
        Assert.NotNull(drawableType);
        Assert.Equal(TypeKind.Interface, drawableType.TypeKind);
        Assert.Single(drawableType.Methods);
    }

    [Fact]
    public void TestEnumDeclaration()
    {
        var source = @"
enum Color:
    RED
    GREEN
    BLUE
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var colorType = symbolTable.LookupType("Color");
        Assert.NotNull(colorType);
        Assert.Equal(TypeKind.Enum, colorType.TypeKind);
    }

    [Fact]
    public void TestMethodDeclaration()
    {
        var source = @"
class Calculator:
    def add(self, a: int, b: int) -> int:
        return a + b

    def subtract(self, a: int, b: int) -> int:
        return a - b
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var calcType = symbolTable.LookupType("Calculator");
        Assert.NotNull(calcType);
        Assert.Equal(2, calcType.Methods.Count);
        Assert.Contains(calcType.Methods, m => m.Name == "add");
        Assert.Contains(calcType.Methods, m => m.Name == "subtract");
    }

    [Fact]
    public void TestBuiltinTypes()
    {
        var source = @"
def test() -> int:
    pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);

        // Verify builtin types are available
        Assert.NotNull(symbolTable.LookupType("int"));
        Assert.NotNull(symbolTable.LookupType("str"));
        Assert.NotNull(symbolTable.LookupType("bool"));
        Assert.NotNull(symbolTable.LookupType("float"));
        Assert.NotNull(symbolTable.LookupType("list"));
        Assert.NotNull(symbolTable.LookupType("dict"));

        // Verify builtin functions
        Assert.NotNull(symbolTable.LookupFunction("print"));
        Assert.NotNull(symbolTable.LookupFunction("len"));
    }

    [Fact]
    public void TestGenericClass()
    {
        var source = @"
class Container[T]:
    value: T

    def get(self) -> T:
        return self.value
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var containerType = symbolTable.LookupType("Container");
        Assert.NotNull(containerType);
        Assert.True(containerType.IsGeneric);
        Assert.Single(containerType.TypeParameters);
        Assert.Equal("T", containerType.TypeParameters[0]);
    }

    [Fact]
    public void TestConstantDeclaration()
    {
        var source = @"
const PI: float = 3.14159
const MAX_SIZE: int = 100
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var piConst = symbolTable.LookupVariable("PI");
        Assert.NotNull(piConst);
        Assert.True(piConst.IsConstant);

        var maxSizeConst = symbolTable.LookupVariable("MAX_SIZE");
        Assert.NotNull(maxSizeConst);
        Assert.True(maxSizeConst.IsConstant);
    }

    #region Edge Cases - Duplicate Definitions

    [Fact]
    public void TestDuplicateStructDefinition()
    {
        var source = @"
struct Point:
    x: int

struct Point:
    y: int
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Single(resolver.Errors);
        Assert.Contains("Struct 'Point' is already defined", resolver.Errors[0].Message);
    }

    [Fact]
    public void TestDuplicateInterfaceDefinition()
    {
        var source = @"
interface IShape:
    pass

interface IShape:
    pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Single(resolver.Errors);
        Assert.Contains("Interface 'IShape' is already defined", resolver.Errors[0].Message);
    }

    [Fact]
    public void TestDuplicateEnumDefinition()
    {
        var source = @"
enum Color:
    RED

enum Color:
    BLUE
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Single(resolver.Errors);
        Assert.Contains("Enum 'Color' is already defined", resolver.Errors[0].Message);
    }

    [Fact]
    public void TestDuplicateFunctionDefinition()
    {
        var source = @"
def calculate(x: int) -> int:
    return x * 2

def calculate(x: int) -> int:
    return x * 3
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Single(resolver.Errors);
        Assert.Contains("Function 'calculate' is already defined", resolver.Errors[0].Message);
    }

    [Fact]
    public void TestDuplicateConstantDefinition()
    {
        var source = @"
const VALUE: int = 10
const VALUE: int = 20
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Single(resolver.Errors);
        Assert.Contains("Constant 'VALUE' is already defined", resolver.Errors[0].Message);
    }

    [Fact]
    public void TestConflictingTypeNames()
    {
        var source = @"
class MyType:
    pass

struct MyType:
    x: int
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Single(resolver.Errors);
        Assert.Contains("Struct 'MyType' is already defined", resolver.Errors[0].Message);
    }

    #endregion

    #region Edge Cases - Nested Scopes

    [Fact]
    public void TestNestedClassesNotSupported()
    {
        // Phase 1 doesn't support nested types in the current implementation
        // This test documents current behavior
        var source = @"
class Outer:
    class Inner:
        pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        // Inner class should not be in global scope
        var innerType = symbolTable.LookupType("Inner");
        Assert.Null(innerType); // Not accessible from global scope in current implementation
    }

    [Fact]
    public void TestMethodsInDifferentClassesDontConflict()
    {
        var source = @"
class ClassA:
    def method(self) -> int:
        return 1

class ClassB:
    def method(self) -> int:
        return 2
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var classA = symbolTable.LookupType("ClassA");
        var classB = symbolTable.LookupType("ClassB");

        Assert.NotNull(classA);
        Assert.NotNull(classB);
        Assert.Single(classA.Methods);
        Assert.Single(classB.Methods);
    }

    #endregion

    #region Edge Cases - Access Levels

    [Fact]
    public void TestDunderMethodsArePublic()
    {
        var source = @"
class MyClass:
    def __init__(self):
        pass

    def __str__(self) -> str:
        return ''
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var classType = symbolTable.LookupType("MyClass");
        Assert.NotNull(classType);

        // Special methods like __init__ should be public, not private
        var initMethod = classType.Methods.First(m => m.Name == "__init__");
        Assert.Equal(AccessLevel.Public, initMethod.AccessLevel);

        var strMethod = classType.Methods.First(m => m.Name == "__str__");
        Assert.Equal(AccessLevel.Public, strMethod.AccessLevel);
    }

    [Fact]
    public void TestMangledNamesArePrivate()
    {
        var source = @"
class MyClass:
    __private_var: int
    def __private_method(self):
        pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var classType = symbolTable.LookupType("MyClass");
        Assert.NotNull(classType);

        var privateField = classType.Fields.First(f => f.Name == "__private_var");
        Assert.Equal(AccessLevel.Private, privateField.AccessLevel);

        var privateMethod = classType.Methods.First(m => m.Name == "__private_method");
        Assert.Equal(AccessLevel.Private, privateMethod.AccessLevel);
    }

    #endregion

    #region Edge Cases - Generic Types

    [Fact]
    public void TestMultipleTypeParameters()
    {
        var source = @"
class Pair[T, U]:
    first: T
    second: U
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var pairType = symbolTable.LookupType("Pair");
        Assert.NotNull(pairType);
        Assert.True(pairType.IsGeneric);
        Assert.Equal(2, pairType.TypeParameters.Count);
        Assert.Equal("T", pairType.TypeParameters[0]);
        Assert.Equal("U", pairType.TypeParameters[1]);
    }

    [Fact]
    public void TestGenericStruct()
    {
        var source = @"
struct Option[T]:
    value: T
    has_value: bool
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var optionType = symbolTable.LookupType("Option");
        Assert.NotNull(optionType);
        Assert.True(optionType.IsGeneric);
        Assert.Equal(TypeKind.Struct, optionType.TypeKind);
        Assert.Single(optionType.TypeParameters);
    }

    [Fact]
    public void TestGenericInterface()
    {
        var source = @"
interface IComparable[T]:
    def compare_to(self, other: T) -> int:
        pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var comparableType = symbolTable.LookupType("IComparable");
        Assert.NotNull(comparableType);
        Assert.True(comparableType.IsGeneric);
        Assert.Equal(TypeKind.Interface, comparableType.TypeKind);
    }

    [Fact]
    public void TestNonGenericClassIsNotGeneric()
    {
        var source = @"
class Simple:
    value: int
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var simpleType = symbolTable.LookupType("Simple");
        Assert.NotNull(simpleType);
        Assert.False(simpleType.IsGeneric);
        Assert.Empty(simpleType.TypeParameters);
    }

    #endregion

    #region Edge Cases - Decorators

    [Fact]
    public void TestStaticMethodDecorator()
    {
        var source = @"
class Math:
    @static
    def add(a: int, b: int) -> int:
        return a + b

    @staticmethod
    def multiply(a: int, b: int) -> int:
        return a * b
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var mathType = symbolTable.LookupType("Math");
        Assert.NotNull(mathType);

        var addMethod = mathType.Methods.First(m => m.Name == "add");
        Assert.True(addMethod.IsStatic);

        var multiplyMethod = mathType.Methods.First(m => m.Name == "multiply");
        Assert.True(multiplyMethod.IsStatic);
    }

    [Fact]
    public void TestAbstractMethodDecorator()
    {
        var source = @"
class Animal:
    @abstract
    def make_sound(self) -> str:
        pass

    @abstractmethod
    def move(self):
        pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var animalType = symbolTable.LookupType("Animal");
        Assert.NotNull(animalType);

        var makeSoundMethod = animalType.Methods.First(m => m.Name == "make_sound");
        Assert.True(makeSoundMethod.IsAbstract);

        var moveMethod = animalType.Methods.First(m => m.Name == "move");
        Assert.True(moveMethod.IsAbstract);
    }

    [Fact]
    public void TestVirtualAndOverrideDecorators()
    {
        var source = @"
class Base:
    @virtual
    def process(self) -> int:
        return 1

class Derived:
    @override
    def process(self) -> int:
        return 2
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var baseType = symbolTable.LookupType("Base");
        var derivedType = symbolTable.LookupType("Derived");

        Assert.NotNull(baseType);
        Assert.NotNull(derivedType);

        var baseProcess = baseType.Methods.First(m => m.Name == "process");
        Assert.True(baseProcess.IsVirtual);

        var derivedProcess = derivedType.Methods.First(m => m.Name == "process");
        Assert.True(derivedProcess.IsOverride);
    }

    [Fact]
    public void TestMultipleDecoratorsOnMethod()
    {
        var source = @"
class MyClass:
    @static
    @virtual
    def special_method() -> int:
        return 42
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var classType = symbolTable.LookupType("MyClass");
        Assert.NotNull(classType);

        var method = classType.Methods.First(m => m.Name == "special_method");
        Assert.True(method.IsStatic);
        Assert.True(method.IsVirtual);
        Assert.False(method.IsAbstract);
        Assert.False(method.IsOverride);
    }

    #endregion

    #region Edge Cases - Empty Declarations

    [Fact]
    public void TestEmptyClass()
    {
        var source = @"
class Empty:
    pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var emptyType = symbolTable.LookupType("Empty");
        Assert.NotNull(emptyType);
        Assert.Empty(emptyType.Fields);
        Assert.Empty(emptyType.Methods);
    }

    [Fact]
    public void TestEmptyInterface()
    {
        var source = @"
interface IMarker:
    pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var markerType = symbolTable.LookupType("IMarker");
        Assert.NotNull(markerType);
        Assert.Equal(TypeKind.Interface, markerType.TypeKind);
        Assert.Empty(markerType.Methods);
    }

    [Fact]
    public void TestEmptyStruct()
    {
        var source = @"
struct Unit:
    pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var unitType = symbolTable.LookupType("Unit");
        Assert.NotNull(unitType);
        Assert.Equal(TypeKind.Struct, unitType.TypeKind);
        Assert.Empty(unitType.Fields);
    }

    #endregion

    #region Edge Cases - Mixed Members

    [Fact]
    public void TestClassWithFieldsAndMethods()
    {
        var source = @"
class Counter:
    count: int
    _internal: int
    __private: bool

    def increment(self):
        pass

    def _protected_method(self):
        pass

    def __private_method(self):
        pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var counterType = symbolTable.LookupType("Counter");
        Assert.NotNull(counterType);
        Assert.Equal(3, counterType.Fields.Count);
        Assert.Equal(3, counterType.Methods.Count);

        // Verify access levels are correctly assigned
        Assert.Equal(AccessLevel.Public, counterType.Fields.First(f => f.Name == "count").AccessLevel);
        Assert.Equal(AccessLevel.Protected, counterType.Fields.First(f => f.Name == "_internal").AccessLevel);
        Assert.Equal(AccessLevel.Private, counterType.Fields.First(f => f.Name == "__private").AccessLevel);
    }

    [Fact]
    public void TestStructWithMethodsAllowed()
    {
        var source = @"
struct Point:
    x: float
    y: float

    def distance(self) -> float:
        return 0.0
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var pointType = symbolTable.LookupType("Point");
        Assert.NotNull(pointType);
        Assert.Equal(2, pointType.Fields.Count);
        Assert.Single(pointType.Methods);
    }

    #endregion

    #region Edge Cases - Module Level

    [Fact]
    public void TestMultipleTopLevelDeclarations()
    {
        var source = @"
const VERSION: str = '1.0'

def helper() -> int:
    return 42

class MyClass:
    value: int

struct Point:
    x: int

interface IService:
    pass

enum Status:
    ACTIVE
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        // Verify all declarations are registered
        Assert.NotNull(symbolTable.LookupVariable("VERSION"));
        Assert.NotNull(symbolTable.LookupFunction("helper"));
        Assert.NotNull(symbolTable.LookupType("MyClass"));
        Assert.NotNull(symbolTable.LookupType("Point"));
        Assert.NotNull(symbolTable.LookupType("IService"));
        Assert.NotNull(symbolTable.LookupType("Status"));
    }

    [Fact]
    public void TestEmptyModule()
    {
        var source = @"";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);
        // Only builtins should be present
        Assert.NotNull(symbolTable.LookupType("int"));
        Assert.NotNull(symbolTable.LookupType("str"));
    }

    #endregion

    #region Edge Cases - Symbol Lookup

    [Fact]
    public void TestLookupNonExistentSymbol()
    {
        var source = @"
class Exists:
    pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        Assert.Null(symbolTable.LookupType("DoesNotExist"));
        Assert.Null(symbolTable.LookupFunction("nonexistent_func"));
        Assert.Null(symbolTable.LookupVariable("nonexistent_var"));
    }

    [Fact]
    public void TestCaseSensitiveSymbols()
    {
        var source = @"
class MyClass:
    pass

class myclass:
    pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var type1 = symbolTable.LookupType("MyClass");
        var type2 = symbolTable.LookupType("myclass");

        Assert.NotNull(type1);
        Assert.NotNull(type2);
        Assert.NotEqual(type1, type2);
    }

    #endregion

    #region Edge Cases - Error Recovery

    [Fact]
    public void TestMultipleErrorsReported()
    {
        var source = @"
class Duplicate:
    pass

class Duplicate:
    pass

def duplicate_func():
    pass

def duplicate_func():
    pass

const DUP: int = 1
const DUP: int = 2
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        // Should report 3 errors (one for each duplicate)
        Assert.Equal(3, resolver.Errors.Count);
    }

    [Fact]
    public void TestErrorDoesNotPreventOtherDeclarations()
    {
        var source = @"
class Valid1:
    pass

class Duplicate:
    pass

class Duplicate:
    pass

class Valid2:
    pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Single(resolver.Errors);

        // Valid declarations should still be registered
        Assert.NotNull(symbolTable.LookupType("Valid1"));
        Assert.NotNull(symbolTable.LookupType("Valid2"));
        Assert.NotNull(symbolTable.LookupType("Duplicate")); // First one is registered
    }

    #endregion

    #region Edge Cases - Import Statements

    [Fact]
    public void TestSimpleImport()
    {
        var source = @"
import math
import sys
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        // Imports are logged but not yet fully implemented in Phase 1
        Assert.Empty(resolver.Errors);
    }

    [Fact]
    public void TestFromImport()
    {
        var source = @"
from math import pi, sqrt
from collections import defaultdict
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        // From-imports are logged but not yet fully implemented in Phase 1
        Assert.Empty(resolver.Errors);
    }

    #endregion

    #region Edge Cases - Special Cases

    [Fact]
    public void TestSingleUnderscoreNotProtected()
    {
        var source = @"
class MyClass:
    def _protected(self):
        pass

    def __double_under(self):
        pass

    def single_method(self):
        pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var classType = symbolTable.LookupType("MyClass");
        Assert.NotNull(classType);

        var protectedMethod = classType.Methods.First(m => m.Name == "_protected");
        Assert.Equal(AccessLevel.Protected, protectedMethod.AccessLevel);

        var privateMethod = classType.Methods.First(m => m.Name == "__double_under");
        Assert.Equal(AccessLevel.Private, privateMethod.AccessLevel);

        var publicMethod = classType.Methods.First(m => m.Name == "single_method");
        Assert.Equal(AccessLevel.Public, publicMethod.AccessLevel);
    }

    [Fact]
    public void TestConstantMustBeConst()
    {
        var source = @"
# Regular variable declaration (not const)
x: int = 10

# Constant declaration
const Y: int = 20
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        // Regular variable should not be in symbol table at module level in Phase 1
        // (only consts, functions, and types are handled at module level)
        var xVar = symbolTable.LookupVariable("x");
        Assert.Null(xVar); // Not a const, so not registered in Phase 1

        var yVar = symbolTable.LookupVariable("Y");
        Assert.NotNull(yVar);
        Assert.True(yVar.IsConstant);
    }

    [Fact]
    public void TestLocationInformationPreserved()
    {
        var source = @"
class MyClass:
    pass

def my_function():
    pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var classType = symbolTable.LookupType("MyClass");
        Assert.NotNull(classType);
        Assert.NotNull(classType.DeclarationLine);
        Assert.NotNull(classType.DeclarationColumn);
        Assert.True(classType.DeclarationLine!.Value > 0);

        var func = symbolTable.LookupFunction("my_function");
        Assert.NotNull(func);
        Assert.NotNull(func.DeclarationLine);
        Assert.NotNull(func.DeclarationColumn);
        Assert.True(func.DeclarationLine!.Value > 0);
    }

    [Fact(Skip = "Parser does not support empty enums with pass statement")]
    public void TestEnumWithoutMembers()
    {
        var source = @"
enum EmptyEnum:
    pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var enumType = symbolTable.LookupType("EmptyEnum");
        Assert.NotNull(enumType);
        Assert.Equal(TypeKind.Enum, enumType.TypeKind);
    }

    [Fact]
    public void TestInterfaceFieldsNotAllowed()
    {
        // Document current behavior: fields in interfaces are not specially handled
        // They would be treated as field declarations
        var source = @"
interface IWithField:
    value: int
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        // Phase 1 doesn't validate that interfaces can't have fields
        // This is acceptable for Phase 1 - validation comes later
        Assert.Empty(resolver.Errors);
    }

    [Fact]
    public void TestSymbolKindIsCorrect()
    {
        var source = @"
class MyClass:
    field: int

    def method(self):
        pass

def function():
    pass

const CONSTANT: int = 42
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var classSymbol = symbolTable.LookupType("MyClass");
        Assert.NotNull(classSymbol);
        Assert.Equal(SymbolKind.Type, classSymbol.Kind);

        var funcSymbol = symbolTable.LookupFunction("function");
        Assert.NotNull(funcSymbol);
        Assert.Equal(SymbolKind.Function, funcSymbol.Kind);

        var constSymbol = symbolTable.LookupVariable("CONSTANT");
        Assert.NotNull(constSymbol);
        Assert.Equal(SymbolKind.Variable, constSymbol.Kind);
    }

    [Fact]
    public void TestNoDecoratorsMeansDefaultFlags()
    {
        var source = @"
class MyClass:
    def normal_method(self):
        pass
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);

        var classType = symbolTable.LookupType("MyClass");
        Assert.NotNull(classType);
        var method = classType.Methods.First();

        Assert.False(method.IsStatic);
        Assert.False(method.IsAbstract);
        Assert.False(method.IsVirtual);
        Assert.False(method.IsOverride);
    }

    #endregion
}
