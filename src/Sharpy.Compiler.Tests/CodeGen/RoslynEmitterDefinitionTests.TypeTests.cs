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
/// Tests for struct, interface, and enum code generation
/// </summary>
public partial class RoslynEmitterDefinitionTests
{
    #region Struct Definition Tests

    [Fact]
    public void GenerateStructDeclaration_SimpleStruct_GeneratesPublicStruct()
    {
        // Arrange
        var structDef = new StructDef
        {
            Name = "Point",
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { structDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public struct Point", code);
    }

    [Fact]
    public void GenerateStructDeclaration_WithFields_GeneratesFieldDeclarations()
    {
        // Arrange
        var structDef = new StructDef
        {
            Name = "Point",
            Body = new List<Statement>
            {
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "double" },
                    InitialValue = null
                },
                new VariableDeclaration
                {
                    Name = "y",
                    Type = new TypeAnnotation { Name = "double" },
                    InitialValue = null
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { structDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public double X;", code);
        Assert.Contains("public double Y;", code);
    }

    [Fact]
    public void GenerateStructDeclaration_WithGenericTypeParameter_GeneratesGenericStruct()
    {
        // Arrange
        var structDef = new StructDef
        {
            Name = "Pair",
            TypeParameters = new List<TypeParameterDef> { new TypeParameterDef { Name = "T1" }, new TypeParameterDef { Name = "T2" } }.ToImmutableArray(),
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { structDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public struct Pair<T1, T2>", code);
    }

    [Fact]
    public void GenerateStructDeclaration_WithConstructor_GeneratesConstructorMethod()
    {
        // Arrange
        var structDef = new StructDef
        {
            Name = "Point",
            Body = new List<Statement>
            {
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "double" },
                    InitialValue = null
                },
                new VariableDeclaration
                {
                    Name = "y",
                    Type = new TypeAnnotation { Name = "double" },
                    InitialValue = null
                },
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null },
                        new Parameter { Name = "x", Type = new TypeAnnotation { Name = "double" } },
                        new Parameter { Name = "y", Type = new TypeAnnotation { Name = "double" } }
                    }.ToImmutableArray(),
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new Assignment
                        {
                            Target = new MemberAccess
                            {
                                Object = new Identifier { Name = "self" },
                                Member = "x"
                            },
                            Value = new Identifier { Name = "x" }
                        },
                        new Assignment
                        {
                            Target = new MemberAccess
                            {
                                Object = new Identifier { Name = "self" },
                                Member = "y"
                            },
                            Value = new Identifier { Name = "y" }
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { structDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public struct Point", code);
        Assert.Contains("public Point(double x, double y)", code);
        Assert.Contains("this.X = x;", code);
        Assert.Contains("this.Y = y;", code);
    }

    [Fact]
    public void GenerateStructDeclaration_WithMethod_GeneratesMethod()
    {
        // Arrange
        var structDef = new StructDef
        {
            Name = "Vector2",
            Body = new List<Statement>
            {
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "double" },
                    InitialValue = null
                },
                new VariableDeclaration
                {
                    Name = "y",
                    Type = new TypeAnnotation { Name = "double" },
                    InitialValue = null
                },
                new FunctionDef
                {
                    Name = "length",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "double" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement
                        {
                            Value = new BinaryOp
                            {
                                Left = new BinaryOp
                                {
                                    Left = new MemberAccess
                                    {
                                        Object = new Identifier { Name = "self" },
                                        Member = "x"
                                    },
                                    Operator = BinaryOperator.Power,
                                    Right = new IntegerLiteral { Value = "2" }
                                },
                                Operator = BinaryOperator.Add,
                                Right = new BinaryOp
                                {
                                    Left = new MemberAccess
                                    {
                                        Object = new Identifier { Name = "self" },
                                        Member = "y"
                                    },
                                    Operator = BinaryOperator.Power,
                                    Right = new IntegerLiteral { Value = "2" }
                                }
                            }
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { structDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public struct Vector2", code);
        Assert.Contains("public double Length()", code);
        Assert.Contains("return", code);
    }

    [Fact]
    public void GenerateStructDeclaration_CompleteVector2_GeneratesCorrectCode()
    {
        // Arrange - Complete Vector2 example from task specification
        var structDef = new StructDef
        {
            Name = "Vector2",
            Body = new List<Statement>
            {
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "float" }, // Sharpy float -> C# double
                    InitialValue = null
                },
                new VariableDeclaration
                {
                    Name = "y",
                    Type = new TypeAnnotation { Name = "float" },
                    InitialValue = null
                },
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null },
                        new Parameter { Name = "x", Type = new TypeAnnotation { Name = "float" } },
                        new Parameter { Name = "y", Type = new TypeAnnotation { Name = "float" } }
                    }.ToImmutableArray(),
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new Assignment
                        {
                            Target = new MemberAccess
                            {
                                Object = new Identifier { Name = "self" },
                                Member = "x"
                            },
                            Value = new Identifier { Name = "x" }
                        },
                        new Assignment
                        {
                            Target = new MemberAccess
                            {
                                Object = new Identifier { Name = "self" },
                                Member = "y"
                            },
                            Value = new Identifier { Name = "y" }
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { structDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public struct Vector2", code);
        Assert.Contains("public double X;", code); // Sharpy float -> C# double
        Assert.Contains("public double Y;", code);
        Assert.Contains("public Vector2(double x, double y)", code);
        Assert.Contains("this.X = x;", code);
        Assert.Contains("this.Y = y;", code);
    }

    [Fact]
    public void GenerateStructDeclaration_WithInterface_GeneratesImplementation()
    {
        // Arrange
        var structDef = new StructDef
        {
            Name = "Point",
            BaseClasses = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "IDrawable" }
            }.ToImmutableArray(),
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "draw",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null }
                    }.ToImmutableArray(),
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new PassStatement()
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { structDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public struct Point : IDrawable", code);
        Assert.Contains("public void Draw()", code);
    }

    #endregion

    #region Interface Definition Tests

    [Fact]
    public void GenerateInterfaceDeclaration_SimpleInterface_GeneratesPublicInterface()
    {
        // Arrange
        var interfaceDef = new InterfaceDef
        {
            Name = "IDrawable",
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { interfaceDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Debug

        // Assert
        Assert.Contains("public interface IDrawable", code);
    }

    [Fact]
    public void GenerateInterfaceDeclaration_WithMethod_GeneratesMethodSignature()
    {
        // Arrange
        var interfaceDef = new InterfaceDef
        {
            Name = "IDrawable",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "draw",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null }
                    }.ToImmutableArray(),
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new ExpressionStatement { Expression = new EllipsisLiteral() }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { interfaceDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("void Draw();", code);
        Assert.DoesNotContain("self", code); // self parameter should be skipped
    }

    [Fact]
    public void GenerateInterfaceDeclaration_WithBaseInterface_GeneratesInheritance()
    {
        // Arrange
        var interfaceDef = new InterfaceDef
        {
            Name = "IShape",
            BaseInterfaces = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "IDrawable" }
            }.ToImmutableArray(),
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { interfaceDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public interface IShape : IDrawable", code);
    }

    [Fact]
    public void GenerateInterfaceDeclaration_WithGenericTypeParameter_GeneratesGenericInterface()
    {
        // Arrange
        var interfaceDef = new InterfaceDef
        {
            Name = "IRepository",
            TypeParameters = new List<TypeParameterDef> { new TypeParameterDef { Name = "T" } }.ToImmutableArray(),
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { interfaceDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public interface IRepository<T>", code);
    }

    #endregion

    #region Enum Definition Tests

    [Fact]
    public void GenerateEnumDeclaration_SimpleEnum_GeneratesPublicEnum()
    {
        // Arrange
        var enumDef = new EnumDef
        {
            Name = "Color",
            Members = new List<EnumMember>
            {
                new EnumMember { Name = "RED", Value = new IntegerLiteral { Value = "1" } },
                new EnumMember { Name = "GREEN", Value = new IntegerLiteral { Value = "2" } },
                new EnumMember { Name = "BLUE", Value = new IntegerLiteral { Value = "3" } }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { enumDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public enum Color", code);
        Assert.Contains("RED = 1", code);
        Assert.Contains("GREEN = 2", code);
        Assert.Contains("BLUE = 3", code);
    }

    [Fact]
    public void GenerateEnumDeclaration_WithoutExplicitValues_GeneratesEnumMembers()
    {
        // Arrange
        var enumDef = new EnumDef
        {
            Name = "Status",
            Members = new List<EnumMember>
            {
                new EnumMember { Name = "PENDING", Value = null },
                new EnumMember { Name = "ACTIVE", Value = null },
                new EnumMember { Name = "COMPLETE", Value = null }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { enumDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public enum Status", code);
        Assert.Contains("PENDING", code);
        Assert.Contains("ACTIVE", code);
        Assert.Contains("COMPLETE", code);
    }

    [Fact]
    public void GenerateEnumDeclaration_WithDocstring_GeneratesXmlDoc()
    {
        // Arrange
        var enumDef = new EnumDef
        {
            Name = "Color",
            Members = new List<EnumMember>
            {
                new EnumMember { Name = "RED", Value = new IntegerLiteral { Value = "1" } }
            }.ToImmutableArray(),
            DocString = "RGB color values"
        };

        // Act
        var module = new Module { Body = new List<Statement> { enumDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("/// <summary>", code);
        Assert.Contains("/// RGB color values", code);
        Assert.Contains("/// </summary>", code);
    }

    [Fact]
    public void GenerateEnumMemberAccess_PreservesScreamingSnakeCase()
    {
        // Arrange: Color.RED -> Color.RED, Color.DARK_BLUE -> Color.DARK_BLUE (#702)
        _context.IsEntryPoint = true;
        var enumDef = new EnumDef
        {
            Name = "Color",
            Members = new List<EnumMember>
            {
                new EnumMember { Name = "RED", Value = new IntegerLiteral { Value = "1" } },
                new EnumMember { Name = "DARK_BLUE", Value = new IntegerLiteral { Value = "2" } }
            }.ToImmutableArray()
        };

        var assignment = new Assignment
        {
            Target = new Identifier { Name = "favorite" },
            Value = new MemberAccess
            {
                Object = new Identifier { Name = "Color" },
                Member = "RED"
            },
            Operator = AssignmentOperator.Assign
        };

        var assignment2 = new Assignment
        {
            Target = new Identifier { Name = "dark" },
            Value = new MemberAccess
            {
                Object = new Identifier { Name = "Color" },
                Member = "DARK_BLUE"
            },
            Operator = AssignmentOperator.Assign
        };

        // Wrap assignments in a main() function (entry points require main())
        var mainFunc = new FunctionDef
        {
            Name = "main",
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = new List<Statement> { assignment, assignment2 }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { enumDef, mainFunc }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("Color.RED", code);
        Assert.Contains("Color.DARK_BLUE", code);
    }

    [Fact]
    public void GenerateEnumValueProperty_GeneratesCastToInt()
    {
        // Arrange: favorite.value -> (int)favorite
        // This test requires proper symbol table setup to detect that 'favorite' is an enum type
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins)
        {
            IsEntryPoint = true
        };
        var emitter = new RoslynEmitter(context);

        var enumDef = new EnumDef
        {
            Name = "Color",
            Members = new List<EnumMember>
            {
                new EnumMember { Name = "RED", Value = new IntegerLiteral { Value = "1" } }
            }.ToImmutableArray()
        };

        // Register the enum type in the symbol table
        var enumTypeSymbol = new TypeSymbol
        {
            Name = "Color",
            Kind = Sharpy.Compiler.Semantic.SymbolKind.Type,
            TypeKind = Sharpy.Compiler.Semantic.TypeKind.Enum
        };
        symbolTable.Define(enumTypeSymbol);

        // Register the 'favorite' variable with the enum type
        var favoriteSymbol = new VariableSymbol
        {
            Name = "favorite",
            Kind = Sharpy.Compiler.Semantic.SymbolKind.Variable,
            Type = new UserDefinedType { Symbol = enumTypeSymbol, Name = "Color" }
        };
        symbolTable.Define(favoriteSymbol);

        var assignment1 = new Assignment
        {
            Target = new Identifier { Name = "favorite" },
            Value = new MemberAccess
            {
                Object = new Identifier { Name = "Color" },
                Member = "RED"
            },
            Operator = AssignmentOperator.Assign
        };

        var assignment2 = new Assignment
        {
            Target = new Identifier { Name = "value" },
            Value = new MemberAccess
            {
                Object = new Identifier { Name = "favorite" },
                Member = "value"
            },
            Operator = AssignmentOperator.Assign
        };

        // Wrap assignments in main() (entry points require main())
        var mainFunc = new FunctionDef
        {
            Name = "main",
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = new List<Statement> { assignment1, assignment2 }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { enumDef, mainFunc }.ToImmutableArray() };
        var compilationUnit = emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("(int)favorite", code);
    }

    #endregion

}
