using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
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
        code.Should().Contain("public Sharpy.Str Describe()");
        code.Should().Contain("return ((Sharpy.Str)\"shape\")");
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

    #region Abstract Class Interface Implementation

    [Fact]
    public void GenerateAbstractClass_ImplementingInterface_GeneratesStubsForMissingMethods()
    {
        // Abstract class declares it implements an interface but doesn't provide the method
        var source = @"
interface IDisplayable:
    def display(self) -> None: ...

@abstract
class Shape(IDisplayable):
    def area(self) -> float: ...
";
        var code = CompileToCSharp(source);

        // Should have the abstract class
        code.Should().Contain("public abstract class Shape : IDisplayable");
        // Should have the explicit abstract method from the class
        code.Should().Contain("public abstract double Area();");
        // Should generate abstract stub for the missing interface method
        code.Should().Contain("public abstract void Display();");
    }

    [Fact]
    public void GenerateAbstractClass_ImplementingInterface_SkipsMethodsAlreadyDefined()
    {
        // Abstract class implements interface AND provides the method
        var source = @"
interface IDisplayable:
    def display(self) -> None: ...

@abstract
class Shape(IDisplayable):
    def display(self) -> None:
        print(""shape"")
";
        var code = CompileToCSharp(source);

        code.Should().Contain("public abstract class Shape : IDisplayable");
        // display() should appear once as a concrete method, not as abstract stub
        code.Should().Contain("public void Display()");
        // Should NOT have duplicate abstract stub
        var abstractDisplayCount = System.Text.RegularExpressions.Regex.Matches(
            code, @"public abstract void Display\(\)").Count;
        abstractDisplayCount.Should().Be(0);
    }

    [Fact]
    public void GenerateAbstractClass_ImplementingMultipleInterfaces_GeneratesAllMissingStubs()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> None: ...

interface ISerializable:
    def serialize(self) -> str: ...

@abstract
class Shape(IDrawable, ISerializable):
    def area(self) -> float: ...
";
        var code = CompileToCSharp(source);

        code.Should().Contain("public abstract class Shape : IDrawable, ISerializable");
        // Should generate stubs for both interface methods
        code.Should().Contain("public abstract void Draw();");
        code.Should().Contain("public abstract Sharpy.Str Serialize();");
    }

    [Fact]
    public void GenerateAbstractClass_InterfaceInheritance_GeneratesStubsForAllMethods()
    {
        // IJsonSerializable extends ISerializable
        var source = @"
interface ISerializable:
    def serialize(self) -> str: ...

interface IJsonSerializable(ISerializable):
    def to_json(self) -> str: ...

@abstract
class Shape(IJsonSerializable):
    def area(self) -> float: ...
";
        var code = CompileToCSharp(source);

        code.Should().Contain("public abstract class Shape : IJsonSerializable");
        // Should generate stub for the method from IJsonSerializable
        code.Should().Contain("public abstract Sharpy.Str ToJson();");
        // Should generate stub for inherited method from ISerializable
        code.Should().Contain("public abstract Sharpy.Str Serialize();");
    }

    [Fact]
    public void GenerateAbstractClass_WithParameters_GeneratesCorrectStubSignature()
    {
        var source = @"
interface IMovable:
    def move(self, x: int, y: int) -> None: ...

@abstract
class Entity(IMovable):
    def update(self) -> None: ...
";
        var code = CompileToCSharp(source);

        code.Should().Contain("public abstract class Entity : IMovable");
        // Stub should have correct parameters
        code.Should().Contain("public abstract void Move(int x, int y);");
    }

    [Fact]
    public void GenerateAbstractClass_SomeMissingMethods_GeneratesOnlyMissingStubs()
    {
        var source = @"
interface IWidget:
    def draw(self) -> None: ...
    def resize(self, width: int, height: int) -> None: ...

@abstract
class BaseWidget(IWidget):
    def draw(self) -> None:
        print(""drawing"")
    # resize is missing, should generate abstract stub
";
        var code = CompileToCSharp(source);

        code.Should().Contain("public abstract class BaseWidget : IWidget");
        // draw is implemented, should be concrete
        code.Should().Contain("public void Draw()");
        // resize is missing, should be abstract stub
        code.Should().Contain("public abstract void Resize(int width, int height);");
    }

    [Fact]
    public void GenerateConcreteClass_ImplementingInterface_NoStubs()
    {
        // Non-abstract class should NOT generate stubs
        // (it would be a compile error in C# if methods are missing, which is correct behavior)
        var source = @"
interface IDisplayable:
    def display(self) -> None: ...

class Circle(IDisplayable):
    def display(self) -> None:
        print(""circle"")
";
        var code = CompileToCSharp(source);

        code.Should().Contain("public class Circle : IDisplayable");
        code.Should().NotContain("abstract");
    }

    #endregion
}
