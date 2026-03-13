using Xunit;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Semantic;

public class NameResolverDocstringTests
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
    public void FunctionWithDocstring_SetsDocumentation()
    {
        var source = @"
def greet(name: str) -> str:
    """"""Say hello to someone.""""""
    return ""hello "" + name
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.False(resolver.Diagnostics.HasErrors);
        var func = symbolTable.Lookup("greet") as FunctionSymbol;
        Assert.NotNull(func);
        Assert.Equal("Say hello to someone.", func.Documentation);
    }

    [Fact]
    public void FunctionWithoutDocstring_DocumentationIsNull()
    {
        var source = @"
def greet(name: str) -> str:
    return ""hello "" + name
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.False(resolver.Diagnostics.HasErrors);
        var func = symbolTable.Lookup("greet") as FunctionSymbol;
        Assert.NotNull(func);
        Assert.Null(func.Documentation);
    }

    [Fact]
    public void ClassWithDocstring_SetsDocumentation()
    {
        var source = @"
class Animal:
    """"""Represents an animal.""""""
    name: str
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.False(resolver.Diagnostics.HasErrors);
        var type = symbolTable.LookupType("Animal");
        Assert.NotNull(type);
        Assert.Equal("Represents an animal.", type.Documentation);
    }

    [Fact]
    public void ClassWithoutDocstring_DocumentationIsNull()
    {
        var source = @"
class Animal:
    name: str
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.False(resolver.Diagnostics.HasErrors);
        var type = symbolTable.LookupType("Animal");
        Assert.NotNull(type);
        Assert.Null(type.Documentation);
    }

    [Fact]
    public void MethodInsideClassWithDocstring_SetsDocumentation()
    {
        var source = @"
class Greeter:
    """"""A greeter class.""""""
    def greet(self, name: str) -> str:
        """"""Greet someone by name.""""""
        return ""hello "" + name
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.False(resolver.Diagnostics.HasErrors);

        var type = symbolTable.LookupType("Greeter");
        Assert.NotNull(type);
        Assert.Equal("A greeter class.", type.Documentation);

        var method = type.Methods.FirstOrDefault(m => m.Name == "greet");
        Assert.NotNull(method);
        Assert.Equal("Greet someone by name.", method.Documentation);
    }

    [Fact]
    public void StructWithDocstring_SetsDocumentation()
    {
        var source = @"
struct Point:
    """"""A 2D point.""""""
    x: int
    y: int
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.False(resolver.Diagnostics.HasErrors);
        var type = symbolTable.LookupType("Point");
        Assert.NotNull(type);
        Assert.Equal("A 2D point.", type.Documentation);
    }

    [Fact]
    public void InterfaceWithDocstring_SetsDocumentation()
    {
        var source = @"
interface Drawable:
    """"""Something that can be drawn.""""""
    def draw(self) -> None:
        ...
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.False(resolver.Diagnostics.HasErrors);
        var type = symbolTable.LookupType("Drawable");
        Assert.NotNull(type);
        Assert.Equal("Something that can be drawn.", type.Documentation);
    }

    [Fact]
    public void EnumWithDocstring_SetsDocumentation()
    {
        var source = @"
enum Color:
    """"""Available colors.""""""
    RED
    GREEN
    BLUE
";
        var (resolver, module, symbolTable) = CreateResolver(source);
        resolver.ResolveDeclarations(module);

        Assert.False(resolver.Diagnostics.HasErrors);
        var type = symbolTable.LookupType("Color");
        Assert.NotNull(type);
        Assert.Equal("Available colors.", type.Documentation);
    }

    [Fact]
    public void ModuleDocstring_IsAvailableOnAstNode()
    {
        var source = @"
""""""This is a module docstring.""""""

def foo() -> None:
    pass
";
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();

        Assert.Equal("This is a module docstring.", module.DocString);
    }
}
