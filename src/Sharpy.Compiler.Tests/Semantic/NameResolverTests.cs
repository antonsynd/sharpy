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
}
