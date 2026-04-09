using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for code generation of definitions (functions, classes, structs, interfaces, enums)
/// </summary>
public partial class RoslynEmitterDefinitionTests
{
    private readonly RoslynEmitter _emitter;
    private readonly CodeGenContext _context;

    public RoslynEmitterDefinitionTests()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        _context = new CodeGenContext(symbolTable, builtins);
        _emitter = new RoslynEmitter(_context);
    }

    #region Function Definition Tests

    [Fact]
    public void GenerateFunctionDeclaration_SimpleFunction_GeneratesPublicStaticMethod()
    {
        // Arrange
        var func = new FunctionDef
        {
            Name = "greet",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = null,
            Body = new List<Statement>
            {
                new ReturnStatement { Value = null }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static void Greet()", code);
    }

    [Fact]
    public void GenerateFunctionDeclaration_WithParameters_GeneratesParameterList()
    {
        // Arrange
        var func = new FunctionDef
        {
            Name = "add",
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "x", Type = new TypeAnnotation { Name = "int" } },
                new Parameter { Name = "y", Type = new TypeAnnotation { Name = "int" } }
            }.ToImmutableArray(),
            ReturnType = new TypeAnnotation { Name = "int" },
            Body = new List<Statement>
            {
                new ReturnStatement { Value = new IntegerLiteral { Value = "0" } }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("int Add(int x, int y)", code);
    }

    [Fact]
    public void GenerateFunctionDeclaration_WithDefaultParameter_GeneratesDefaultValue()
    {
        // Arrange
        var func = new FunctionDef
        {
            Name = "greet",
            Parameters = new List<Parameter>
            {
                new Parameter
                {
                    Name = "name",
                    Type = new TypeAnnotation { Name = "str" },
                    DefaultValue = new StringLiteral { Value = "World" }
                }
            }.ToImmutableArray(),
            ReturnType = new TypeAnnotation { Name = "str" },
            Body = new List<Statement>
            {
                new ReturnStatement { Value = new StringLiteral { Value = "Hello" } }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("string Greet(string name = \"World\")", code);
    }

    [Fact]
    public void GenerateFunctionDeclaration_WithDocstring_GeneratesXmlDoc()
    {
        // Arrange
        var func = new FunctionDef
        {
            Name = "greet",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = new TypeAnnotation { Name = "string" },
            Body = new List<Statement>
            {
                new ReturnStatement { Value = new StringLiteral { Value = "Hello" } }
            }.ToImmutableArray(),
            DocString = "Greets the world"
        };

        // Act
        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("/// <summary>", code);
        Assert.Contains("/// Greets the world", code);
        Assert.Contains("/// </summary>", code);
    }

    [Fact]
    public void GenerateFunctionDeclaration_WithPrivateDecorator_GeneratesPrivateMethod()
    {
        // Arrange
        var func = new FunctionDef
        {
            Name = "helper",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = null,
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray(),
            Decorators = new List<Decorator>
            {
                new Decorator { QualifiedParts = ImmutableArray.Create("private") }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("private static void Helper()", code);
    }

    [Fact]
    public void GenerateFunctionDeclaration_ExampleFromSpec_GeneratesCorrectSignature()
    {
        // Example from spec: def add(a: int, b: int = 1) -> int: return a * b
        // Should generate: public static int Add(int a, int b = 1)
        var func = new FunctionDef
        {
            Name = "add",
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "a", Type = new TypeAnnotation { Name = "int" } },
                new Parameter
                {
                    Name = "b",
                    Type = new TypeAnnotation { Name = "int" },
                    DefaultValue = new IntegerLiteral { Value = "1" }
                }
            }.ToImmutableArray(),
            ReturnType = new TypeAnnotation { Name = "int" },
            Body = new List<Statement>
            {
                new ReturnStatement
                {
                    Value = new BinaryOp
                    {
                        Left = new Identifier { Name = "a" },
                        Operator = BinaryOperator.Multiply,
                        Right = new Identifier { Name = "b" }
                    }
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static int Add(int a, int b = 1)", code);
        Assert.Contains("return a * b;", code);
    }

    [Fact]
    public void GenerateFunctionDeclaration_WithSingleTypeParameter_GeneratesGenericMethod()
    {
        // Arrange
        var func = new FunctionDef
        {
            Name = "identity",
            TypeParameters = new List<TypeParameterDef> { new TypeParameterDef { Name = "T" } }.ToImmutableArray(),
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "value", Type = new TypeAnnotation { Name = "T" } }
            }.ToImmutableArray(),
            ReturnType = new TypeAnnotation { Name = "T" },
            Body = new List<Statement>
            {
                new ReturnStatement { Value = new Identifier { Name = "value" } }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static T Identity<T>(T value)", code);
    }

    [Fact]
    public void GenerateFunctionDeclaration_WithMultipleTypeParameters_GeneratesGenericMethod()
    {
        // Arrange
        var func = new FunctionDef
        {
            Name = "find_max",
            TypeParameters = new List<TypeParameterDef> { new TypeParameterDef { Name = "T" }, new TypeParameterDef { Name = "U" } }.ToImmutableArray(),
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "a", Type = new TypeAnnotation { Name = "T" } },
                new Parameter { Name = "b", Type = new TypeAnnotation { Name = "U" } }
            }.ToImmutableArray(),
            ReturnType = new TypeAnnotation { Name = "T" },
            Body = new List<Statement>
            {
                new ReturnStatement { Value = new Identifier { Name = "a" } }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static T FindMax<T, U>(T a, U b)", code);
    }

    [Fact]
    public void GenerateFunctionDeclaration_GenericWithListParameter_GeneratesGenericMethod()
    {
        // Arrange
        var func = new FunctionDef
        {
            Name = "get_first",
            TypeParameters = new List<TypeParameterDef> { new TypeParameterDef { Name = "T" } }.ToImmutableArray(),
            Parameters = new List<Parameter>
            {
                new Parameter
                {
                    Name = "items",
                    Type = new TypeAnnotation
                    {
                        Name = "list",
                        TypeArguments = new List<TypeAnnotation>
                        {
                            new TypeAnnotation { Name = "T" }
                        }.ToImmutableArray()
                    }
                }
            }.ToImmutableArray(),
            ReturnType = new TypeAnnotation { Name = "T" },
            Body = new List<Statement>
            {
                new ReturnStatement
                {
                    Value = new IndexAccess
                    {
                        Object = new Identifier { Name = "items" },
                        Index = new IntegerLiteral { Value = "0" }
                    }
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        // Check for method signature (name mangling converts get_first to GetFirst)
        // Note: Sharpy's list[T] maps to Sharpy.List<T>
        Assert.Contains("T GetFirst<T>(Sharpy.List<T> items)", code);
    }

    #endregion

}
