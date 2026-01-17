using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Xunit;
using FluentAssertions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for code generation of abstract methods using both explicit @abstract decorator
/// and implicit abstract (ellipsis body in @abstract class).
/// </summary>
public class RoslynEmitterAbstractTests
{
    private string CompileToCSharp(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        // Name resolution
        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        // Type checking
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        typeChecker.CheckModule(module);

        // Code generation
        var context = new CodeGenContext(symbolTable, builtinRegistry);
        var emitter = new RoslynEmitter(context);
        var compilationUnit = emitter.GenerateCompilationUnit(module);

        return compilationUnit.NormalizeWhitespace().ToFullString();
    }

    [Fact]
    public void GenerateAbstractMethod_ImplicitAbstract_NoBody()
    {
        // Method with ellipsis in @abstract class - should have no body
        var source = @"
@abstract
class Shape:
    def area(self) -> float: ...
";
        var code = CompileToCSharp(source);

        code.Should().Contain("public abstract class Shape");
        code.Should().Contain("public abstract double Area();");
        code.Should().NotContain("NotImplementedException");
    }

    [Fact]
    public void GenerateAbstractMethod_ExplicitDecorator_NoBody()
    {
        var source = @"
@abstract
class Shape:
    @abstract
    def area(self) -> float: ...
";
        var code = CompileToCSharp(source);

        code.Should().Contain("public abstract double Area();");
        code.Should().NotContain("NotImplementedException");
    }

    [Fact]
    public void GenerateConcreteMethod_WithEllipsis_ThrowsNotImplementedException()
    {
        var source = @"
class TodoService:
    def not_done(self) -> int: ...
";
        var code = CompileToCSharp(source);

        code.Should().Contain("NotImplementedException");
        code.Should().NotContain("abstract");
    }

    [Fact]
    public void GenerateInterfaceMethod_WithInlineEllipsis_NoBody()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> None: ...
";
        var code = CompileToCSharp(source);

        code.Should().Contain("void Draw();");
        code.Should().NotContain("NotImplementedException");
    }

    [Fact]
    public void GenerateAbstractClass_MultipleImplicitAbstractMethods()
    {
        var source = @"
@abstract
class Shape:
    def area(self) -> float: ...
    def perimeter(self) -> float: ...
";
        var code = CompileToCSharp(source);

        code.Should().Contain("public abstract class Shape");
        code.Should().Contain("public abstract double Area();");
        code.Should().Contain("public abstract double Perimeter();");
    }

    [Fact]
    public void GenerateAbstractClass_MixedAbstractAndConcrete()
    {
        var source = @"
@abstract
class Shape:
    def area(self) -> float: ...

    def describe(self) -> str:
        return ""shape""
";
        var code = CompileToCSharp(source);

        code.Should().Contain("public abstract class Shape");
        code.Should().Contain("public abstract double Area();");
        code.Should().Contain("public string Describe()");
        code.Should().Contain("return \"shape\"");
    }

    [Fact]
    public void GenerateInterfaceMethod_WithMultilineEllipsis_NoBody()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> None:
        ...
";
        var code = CompileToCSharp(source);

        code.Should().Contain("void Draw();");
        code.Should().NotContain("NotImplementedException");
    }
}
