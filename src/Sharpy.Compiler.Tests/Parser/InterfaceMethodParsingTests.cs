using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Xunit;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Tests for interface method signature parsing.
/// Verifies that interface methods can be declared with or without explicit ellipsis body.
/// </summary>
public class InterfaceMethodParsingTests
{
    [Fact]
    public void InterfaceMethod_WithExplicitEllipsis_Parses()
    {
        // Arrange: Traditional Python typing.Protocol style
        var source = @"
interface IProcessor:
    def process(self) -> str: ...
";

        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);

        // Act
        var module = parser.ParseModule();

        // Assert
        Assert.Single(module.Body);
        var interfaceDef = Assert.IsType<InterfaceDef>(module.Body[0]);
        Assert.Equal("IProcessor", interfaceDef.Name);
        Assert.Single(interfaceDef.Body);

        var methodDef = Assert.IsType<FunctionDef>(interfaceDef.Body[0]);
        Assert.Equal("process", methodDef.Name);
        Assert.Single(methodDef.Body);

        var exprStmt = Assert.IsType<ExpressionStatement>(methodDef.Body[0]);
        Assert.IsType<EllipsisLiteral>(exprStmt.Expression);
    }

    [Fact]
    public void InterfaceMethod_WithoutColon_Parses()
    {
        // Arrange: New simplified syntax without colon
        var source = @"
interface IProcessor:
    def process(self) -> str
";

        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);

        // Act
        var module = parser.ParseModule();

        // Assert
        Assert.Single(module.Body);
        var interfaceDef = Assert.IsType<InterfaceDef>(module.Body[0]);
        Assert.Equal("IProcessor", interfaceDef.Name);
        Assert.Single(interfaceDef.Body);

        var methodDef = Assert.IsType<FunctionDef>(interfaceDef.Body[0]);
        Assert.Equal("process", methodDef.Name);
        Assert.Single(methodDef.Body);

        // Should have synthesized ellipsis body
        var exprStmt = Assert.IsType<ExpressionStatement>(methodDef.Body[0]);
        Assert.IsType<EllipsisLiteral>(exprStmt.Expression);
    }

    [Fact]
    public void InterfaceMethod_WithoutReturnType_Parses()
    {
        // Arrange: Method without return type annotation
        var source = @"
interface IProcessor:
    def process(self)
";

        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);

        // Act
        var module = parser.ParseModule();

        // Assert
        Assert.Single(module.Body);
        var interfaceDef = Assert.IsType<InterfaceDef>(module.Body[0]);
        Assert.Single(interfaceDef.Body);

        var methodDef = Assert.IsType<FunctionDef>(interfaceDef.Body[0]);
        Assert.Equal("process", methodDef.Name);
        Assert.Null(methodDef.ReturnType);
        Assert.Single(methodDef.Body);

        // Should have synthesized ellipsis body
        var exprStmt = Assert.IsType<ExpressionStatement>(methodDef.Body[0]);
        Assert.IsType<EllipsisLiteral>(exprStmt.Expression);
    }

    [Fact]
    public void InterfaceMethod_MultipleSignatures_Parses()
    {
        // Arrange: Multiple method signatures in one interface
        var source = @"
interface ICollection[T]:
    def add(self, item: T)
    def remove(self, item: T) -> bool
    def clear(self) -> None: ...
    def count(self) -> int
";

        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);

        // Act
        var module = parser.ParseModule();

        // Assert
        Assert.Single(module.Body);
        var interfaceDef = Assert.IsType<InterfaceDef>(module.Body[0]);
        Assert.Equal("ICollection", interfaceDef.Name);
        Assert.Equal(4, interfaceDef.Body.Length);

        // All methods should have ellipsis bodies
        foreach (var stmt in interfaceDef.Body)
        {
            var methodDef = Assert.IsType<FunctionDef>(stmt);
            Assert.Single(methodDef.Body);
            var exprStmt = Assert.IsType<ExpressionStatement>(methodDef.Body[0]);
            Assert.IsType<EllipsisLiteral>(exprStmt.Expression);
        }
    }

    [Fact]
    public void InterfaceMethod_WithDefaultImpl_Parses()
    {
        // Arrange: Interface with a default implementation (still uses colon + body)
        var source = @"
interface IGreeter:
    def greet(self) -> str:
        return ""Hello""
";

        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);

        // Act
        var module = parser.ParseModule();

        // Assert
        Assert.Single(module.Body);
        var interfaceDef = Assert.IsType<InterfaceDef>(module.Body[0]);
        Assert.Single(interfaceDef.Body);

        var methodDef = Assert.IsType<FunctionDef>(interfaceDef.Body[0]);
        Assert.Equal("greet", methodDef.Name);
        Assert.Single(methodDef.Body);

        // Should have actual return statement, not ellipsis
        Assert.IsType<ReturnStatement>(methodDef.Body[0]);
    }

    [Fact]
    public void ClassMethod_StillRequiresColonAndBody()
    {
        // Arrange: Class methods should still require colon and body
        var source = @"
class Processor:
    def process(self) -> str
";

        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);

        // Act: Parse collects errors into Diagnostics instead of throwing
        parser.ParseModule();

        // Assert: Should report error because class methods require body
        parser.Diagnostics.HasErrors.Should().BeTrue("Class methods require colon and body");
    }
}
