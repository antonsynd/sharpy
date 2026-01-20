using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for interface code generation
/// </summary>
public class RoslynEmitterInterfaceTests
{
    private RoslynEmitter CreateEmitter()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins);
        return new RoslynEmitter(context);
    }

    [Fact]
    public void GenerateInterface_SimpleInterface_GeneratesCorrectDeclaration()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new InterfaceDef
                {
                    Name = "IDrawable",
                    TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
                    BaseInterfaces = ImmutableArray<TypeAnnotation>.Empty,
                    Body = new List<Statement>
                    {
                        new FunctionDef
                        {
                            Name = "draw",
                            Parameters = new List<Parameter>
                            {
                                new Parameter { Name = "self" }
                            }.ToImmutableArray(),
                            ReturnType = new TypeAnnotation { Name = "None" },
                            Body = new List<Statement>
                            {
                                new ExpressionStatement
                                {
                                    Expression = new EllipsisLiteral()
                                }
                            }.ToImmutableArray()
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("public interface IDrawable", code);
        Assert.Contains("void Draw();", code);
    }

    [Fact]
    public void GenerateInterface_WithMethodReturnType_GeneratesCorrectSignature()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new InterfaceDef
                {
                    Name = "IShape",
                    TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
                    BaseInterfaces = ImmutableArray<TypeAnnotation>.Empty,
                    Body = new List<Statement>
                    {
                        new FunctionDef
                        {
                            Name = "get_area",
                            Parameters = new List<Parameter>
                            {
                                new Parameter { Name = "self" }
                            }.ToImmutableArray(),
                            ReturnType = new TypeAnnotation { Name = "float" },
                            Body = new List<Statement>
                            {
                                new ExpressionStatement
                                {
                                    Expression = new EllipsisLiteral()
                                }
                            }.ToImmutableArray()
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("public interface IShape", code);
        Assert.Contains("double GetArea();", code);
    }

    [Fact]
    public void GenerateInterface_WithMethodParameters_GeneratesCorrectSignature()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new InterfaceDef
                {
                    Name = "IMovable",
                    TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
                    BaseInterfaces = ImmutableArray<TypeAnnotation>.Empty,
                    Body = new List<Statement>
                    {
                        new FunctionDef
                        {
                            Name = "move",
                            Parameters = new List<Parameter>
                            {
                                new Parameter { Name = "self" },
                                new Parameter
                                {
                                    Name = "x",
                                    Type = new TypeAnnotation { Name = "int" }
                                },
                                new Parameter
                                {
                                    Name = "y",
                                    Type = new TypeAnnotation { Name = "int" }
                                }
                            }.ToImmutableArray(),
                            ReturnType = new TypeAnnotation { Name = "None" },
                            Body = new List<Statement>
                            {
                                new ExpressionStatement
                                {
                                    Expression = new EllipsisLiteral()
                                }
                            }.ToImmutableArray()
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("public interface IMovable", code);
        Assert.Contains("void Move(int x, int y);", code);
    }

    [Fact]
    public void GenerateInterface_WithProperty_GeneratesPropertyWithGetSet()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new InterfaceDef
                {
                    Name = "IEntity",
                    TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
                    BaseInterfaces = ImmutableArray<TypeAnnotation>.Empty,
                    Body = new List<Statement>
                    {
                        new VariableDeclaration
                        {
                            Name = "name",
                            Type = new TypeAnnotation { Name = "str" },
                            InitialValue = null
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("public interface IEntity", code);
        Assert.Contains("string Name", code);
        Assert.Contains("get;", code);
        Assert.Contains("set;", code);
    }

    [Fact]
    public void GenerateInterface_WithMultipleMembers_GeneratesAllMembers()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new InterfaceDef
                {
                    Name = "IGameObject",
                    TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
                    BaseInterfaces = ImmutableArray<TypeAnnotation>.Empty,
                    Body = new List<Statement>
                    {
                        new VariableDeclaration
                        {
                            Name = "position",
                            Type = new TypeAnnotation { Name = "Vector3" },
                            InitialValue = null
                        },
                        new FunctionDef
                        {
                            Name = "update",
                            Parameters = new List<Parameter>
                            {
                                new Parameter { Name = "self" },
                                new Parameter
                                {
                                    Name = "delta_time",
                                    Type = new TypeAnnotation { Name = "float" }
                                }
                            }.ToImmutableArray(),
                            ReturnType = new TypeAnnotation { Name = "None" },
                            Body = new List<Statement>
                            {
                                new ExpressionStatement
                                {
                                    Expression = new EllipsisLiteral()
                                }
                            }.ToImmutableArray()
                        },
                        new FunctionDef
                        {
                            Name = "render",
                            Parameters = new List<Parameter>
                            {
                                new Parameter { Name = "self" }
                            }.ToImmutableArray(),
                            ReturnType = new TypeAnnotation { Name = "None" },
                            Body = new List<Statement>
                            {
                                new ExpressionStatement
                                {
                                    Expression = new EllipsisLiteral()
                                }
                            }.ToImmutableArray()
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("public interface IGameObject", code);
        Assert.Contains("Vector3 Position", code);
        Assert.Contains("void Update(double deltaTime);", code);
        Assert.Contains("void Render();", code);
    }

    [Fact]
    public void GenerateInterface_WithBaseInterface_GeneratesInheritance()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new InterfaceDef
                {
                    Name = "IClickable",
                    TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
                    BaseInterfaces = new List<TypeAnnotation>
                    {
                        new TypeAnnotation { Name = "IDrawable" }
                    }.ToImmutableArray(),
                    Body = new List<Statement>
                    {
                        new FunctionDef
                        {
                            Name = "on_click",
                            Parameters = new List<Parameter>
                            {
                                new Parameter { Name = "self" }
                            }.ToImmutableArray(),
                            ReturnType = new TypeAnnotation { Name = "None" },
                            Body = new List<Statement>
                            {
                                new ExpressionStatement
                                {
                                    Expression = new EllipsisLiteral()
                                }
                            }.ToImmutableArray()
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("public interface IClickable : IDrawable", code);
        Assert.Contains("void OnClick();", code);
    }

    [Fact]
    public void GenerateInterface_WithMultipleBaseInterfaces_GeneratesMultipleInheritance()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new InterfaceDef
                {
                    Name = "IUIElement",
                    TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
                    BaseInterfaces = new List<TypeAnnotation>
                    {
                        new TypeAnnotation { Name = "IDrawable" },
                        new TypeAnnotation { Name = "IClickable" }
                    }.ToImmutableArray(),
                    Body = new List<Statement>
                    {
                        new FunctionDef
                        {
                            Name = "focus",
                            Parameters = new List<Parameter>
                            {
                                new Parameter { Name = "self" }
                            }.ToImmutableArray(),
                            ReturnType = new TypeAnnotation { Name = "None" },
                            Body = new List<Statement>
                            {
                                new ExpressionStatement
                                {
                                    Expression = new EllipsisLiteral()
                                }
                            }.ToImmutableArray()
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("public interface IUIElement : IDrawable, IClickable", code);
        Assert.Contains("void Focus();", code);
    }

    [Fact]
    public void GenerateInterface_WithGenericTypeParameter_GeneratesGenericInterface()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new InterfaceDef
                {
                    Name = "IRepository",
                    TypeParameters = new List<TypeParameterDef> { new TypeParameterDef { Name = "T" } }.ToImmutableArray(),
                    BaseInterfaces = ImmutableArray<TypeAnnotation>.Empty,
                    Body = new List<Statement>
                    {
                        new FunctionDef
                        {
                            Name = "get",
                            Parameters = new List<Parameter>
                            {
                                new Parameter { Name = "self" },
                                new Parameter
                                {
                                    Name = "id",
                                    Type = new TypeAnnotation { Name = "int" }
                                }
                            }.ToImmutableArray(),
                            ReturnType = new TypeAnnotation { Name = "T" },
                            Body = new List<Statement>
                            {
                                new ExpressionStatement
                                {
                                    Expression = new EllipsisLiteral()
                                }
                            }.ToImmutableArray()
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("public interface IRepository<T>", code);
        Assert.Contains("T Get(int id);", code);
    }

    [Fact]
    public void GenerateInterface_WithDocstring_GeneratesXmlDocComment()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new InterfaceDef
                {
                    Name = "IDrawable",
                    TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
                    BaseInterfaces = ImmutableArray<TypeAnnotation>.Empty,
                    DocString = "Represents an object that can be drawn on screen",
                    Body = new List<Statement>
                    {
                        new FunctionDef
                        {
                            Name = "draw",
                            Parameters = new List<Parameter>
                            {
                                new Parameter { Name = "self" }
                            }.ToImmutableArray(),
                            ReturnType = new TypeAnnotation { Name = "None" },
                            DocString = "Draws the object",
                            Body = new List<Statement>
                            {
                                new ExpressionStatement
                                {
                                    Expression = new EllipsisLiteral()
                                }
                            }.ToImmutableArray()
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("/// <summary>", code);
        Assert.Contains("/// Represents an object that can be drawn on screen", code);
        Assert.Contains("/// Draws the object", code);
    }

    [Fact]
    public void GenerateInterface_NamePreservesIPrefix_MaintainsInterfaceNamingConvention()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new InterfaceDef
                {
                    Name = "IDrawable",
                    TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
                    BaseInterfaces = ImmutableArray<TypeAnnotation>.Empty,
                    Body = ImmutableArray<Statement>.Empty
                }
            }.ToImmutableArray()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Interfaces preserve exact casing (no transformation)
        Assert.Contains("public interface IDrawable", code);
    }
}
