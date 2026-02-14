using System.Collections.Immutable;
using Xunit;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sharpy.Compiler.Tests.CodeGen;

public class RoslynEmitterOperatorTests
{
    private readonly RoslynEmitter _emitter;
    private readonly CodeGenContext _context;

    public RoslynEmitterOperatorTests()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        _context = new CodeGenContext(symbolTable, builtins);
        _emitter = new RoslynEmitter(_context);
    }

    [Fact]
    public void GenerateClass_WithAddOperator_GeneratesOperatorOverload()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Vector",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__add__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null },
                        new() { Name = "other", Type = new TypeAnnotation { Name = "Vector" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "Vector" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement
                        {
                            Value = new Identifier { Name = "self" }
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static Vector operator +(Vector left, Vector right)", code);
    }

    [Fact]
    public void GenerateClass_WithSubOperator_GeneratesOperatorOverload()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Number",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__sub__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null },
                        new() { Name = "other", Type = new TypeAnnotation { Name = "Number" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "Number" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new Identifier { Name = "self" } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static Number operator -(Number left, Number right)", code);
    }

    [Fact]
    public void GenerateClass_WithMulOperator_GeneratesOperatorOverload()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Matrix",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__mul__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null },
                        new() { Name = "other", Type = new TypeAnnotation { Name = "Matrix" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "Matrix" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new Identifier { Name = "self" } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static Matrix operator *(Matrix left, Matrix right)", code);
    }

    [Fact]
    public void GenerateClass_WithEqualityOperator_GeneratesOperatorOverloadAndMethod()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Point",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__eq__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null },
                        new() { Name = "other", Type = new TypeAnnotation { Name = "object" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "bool" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new BooleanLiteral { Value = true } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        // Should have both operator== and Equals() override
        Assert.Contains("public static bool operator ==(Point left", code);
        Assert.Contains("public override bool Equals(object", code);
    }

    [Fact]
    public void GenerateClass_WithLessThanOperator_GeneratesOperatorOverload()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Version",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__lt__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null },
                        new() { Name = "other", Type = new TypeAnnotation { Name = "Version" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "bool" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new BooleanLiteral { Value = false } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static bool operator <(Version left, Version right)", code);
    }

    [Fact]
    public void GenerateClass_WithUnaryNegOperator_GeneratesOperatorOverload()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Complex",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__neg__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "Complex" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new Identifier { Name = "self" } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static Complex operator -(Complex value)", code);
    }

    [Fact]
    public void GenerateClass_WithBitwiseAndOperator_GeneratesOperatorOverload()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Flags",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__and__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null },
                        new() { Name = "other", Type = new TypeAnnotation { Name = "Flags" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "Flags" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new Identifier { Name = "self" } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static Flags operator &(Flags left, Flags right)", code);
    }

    [Fact]
    public void GenerateClass_WithToStringMethod_GeneratesOverride()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Person",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__str__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null }
                    }.ToImmutableArray(),
                    Body = new List<Statement>
                    {
                        new ReturnStatement
                        {
                            Value = new StringLiteral { Value = "Person" }
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public override string ToString()", code);
    }

    [Fact]
    public void GenerateClass_WithGetHashCodeMethod_GeneratesOverride()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "HashableItem",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__hash__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null }
                    }.ToImmutableArray(),
                    Body = new List<Statement>
                    {
                        new ReturnStatement
                        {
                            Value = new IntegerLiteral { Value = "42" }
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public override int GetHashCode()", code);
    }

    [Fact]
    public void GenerateClass_WithMultipleOperators_GeneratesAllOverloads()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Number",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__add__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null },
                        new() { Name = "other", Type = new TypeAnnotation { Name = "Number" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "Number" },
                    Body = new List<Statement> { new ReturnStatement { Value = new Identifier { Name = "self" } } }.ToImmutableArray()
                },
                new FunctionDef
                {
                    Name = "__sub__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null },
                        new() { Name = "other", Type = new TypeAnnotation { Name = "Number" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "Number" },
                    Body = new List<Statement> { new ReturnStatement { Value = new Identifier { Name = "self" } } }.ToImmutableArray()
                },
                new FunctionDef
                {
                    Name = "__eq__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null },
                        new() { Name = "other", Type = new TypeAnnotation { Name = "object" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "bool" },
                    Body = new List<Statement> { new ReturnStatement { Value = new BooleanLiteral { Value = true } } }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static Number operator +(Number left, Number right)", code);
        Assert.Contains("public static Number operator -(Number left, Number right)", code);
        Assert.Contains("public static bool operator ==(Number left", code);
        Assert.Contains("public override bool Equals(object", code);
    }

    [Fact]
    public void GenerateClass_WithAddOperator_GeneratesMethodAndOperator()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Vector",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__add__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null },
                        new() { Name = "other", Type = new TypeAnnotation { Name = "Vector" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "Vector" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new Identifier { Name = "self" } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert - should have only the inlined operator (no instance method)
        Assert.DoesNotContain("__Add__", code);
        Assert.Contains("public static Vector operator +(Vector left, Vector right)", code);
        // The body should reference 'left' (self replacement) not 'this'
        Assert.Contains("left", code);
    }

    [Fact]
    public void GenerateClass_WithEqOnly_GeneratesComplementaryNotEquals()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Point",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__eq__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null },
                        new() { Name = "other", Type = new TypeAnnotation { Name = "object" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "bool" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new BooleanLiteral { Value = true } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert - should have both operator == and complementary operator !=
        Assert.Contains("public static bool operator ==(Point left", code);
        Assert.Contains("public static bool operator !=(Point left", code);
    }

    [Fact]
    public void GenerateClass_WithTypedEqOverload_GeneratesEqualsWithoutOverride()
    {
        // __eq__(self, other: Point) should generate Equals(Point), NOT override Equals(object)
        var classDef = new ClassDef
        {
            Name = "Point",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__eq__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null },
                        new() { Name = "other", Type = new TypeAnnotation { Name = "Point" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "bool" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new BooleanLiteral { Value = true } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert - should have Equals(Point) without override, and operator==
        Assert.Contains("public bool Equals(Point", code);
        Assert.DoesNotContain("override bool Equals", code);
        Assert.Contains("public static bool operator ==(Point left, Point right)", code);
    }

    [Fact]
    public void GenerateClass_WithNeOnly_GeneratesComplementaryEquals()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Point",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__ne__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null },
                        new() { Name = "other", Type = new TypeAnnotation { Name = "Point" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "bool" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new BooleanLiteral { Value = false } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert - should have both operator != and complementary operator ==
        Assert.Contains("public static bool operator !=(Point left", code);
        Assert.Contains("public static bool operator ==(Point left", code);
    }

    [Fact]
    public void GenerateClass_WithBoolDunder_GeneratesOperatorTrueAndFalse()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Truthy",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__bool__",
                    Parameters = new List<Parameter>
                    {
                        new() { Name = "self", Type = null }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "bool" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement
                        {
                            Value = new BooleanLiteral { Value = true }
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert - should have IsTrue property, operator true, and operator false
        Assert.Contains("public bool IsTrue", code);
        Assert.Contains("operator true", code);
        Assert.Contains("operator false", code);
        // operator true/false reference IsTrue property
        Assert.Contains("value.IsTrue", code);
        Assert.Contains("!value.IsTrue", code);
        // No __Bool__ method should be generated
        Assert.DoesNotContain("__Bool__", code);
    }
}
