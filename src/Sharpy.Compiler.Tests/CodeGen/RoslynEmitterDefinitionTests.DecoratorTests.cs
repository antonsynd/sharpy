using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for decorator and type constraint code generation
/// </summary>
public partial class RoslynEmitterDefinitionTests
{
    #region Decorator Tests

    [Fact]
    public void GenerateClassDeclaration_WithAbstractDecorator_GeneratesAbstractClass()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Shape",
            Decorators = new List<Decorator>
            {
                new Decorator { Name = "abstract" }
            }.ToImmutableArray(),
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public abstract class Shape", code);
    }

    [Fact]
    public void GenerateMethod_WithAbstractMethodDecorator_GeneratesAbstractMethodWithoutBody()
    {
        // Arrange - Abstract method with ellipsis should have no body
        var classDef = new ClassDef
        {
            Name = "Shape",
            Decorators = new List<Decorator>
            {
                new Decorator { Name = "abstract" }
            }.ToImmutableArray(),
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "area",
                    Decorators = new List<Decorator>
                    {
                        new Decorator { Name = "abstract" }
                    }.ToImmutableArray(),
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
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public abstract class Shape", code);
        // Per spec: Sharpy 'float' -> C# 'double'
        Assert.Contains("public abstract double Area();", code);
        // Abstract method should NOT have a body (no braces)
        Assert.DoesNotContain("Area()\r\n{", code);
        Assert.DoesNotContain("Area()\n{", code);
        Assert.DoesNotContain("NotImplementedException", code);
    }

    [Fact]
    public void GenerateMethod_ConcreteMethodWithEllipsis_ThrowsNotImplementedException()
    {
        // Arrange - Concrete method with ellipsis should throw NotImplementedException
        var classDef = new ClassDef
        {
            Name = "Todo",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "not_yet_implemented",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "int" },
                    Body = new List<Statement>
                    {
                        new ExpressionStatement
                        {
                            Expression = new EllipsisLiteral()
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
        Assert.Contains("public int NotYetImplemented()", code);
        Assert.Contains("throw new System.NotImplementedException()", code);
    }

    [Fact]
    public void GenerateMethod_WithStaticDecorator_GeneratesStaticMethod()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Math",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "add",
                    Decorators = new List<Decorator>
                    {
                        new Decorator { Name = "staticmethod" }
                    }.ToImmutableArray(),
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
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static int Add(int x, int y)", code);
    }

    [Fact]
    public void GenerateMethod_WithoutSelfParameter_GeneratesStaticMethod()
    {
        // Arrange - Method without 'self' parameter should be static (primary mechanism)
        var classDef = new ClassDef
        {
            Name = "Calculator",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "multiply",
                    Decorators = ImmutableArray<Decorator>.Empty, // No @static decorator needed!
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "a", Type = new TypeAnnotation { Name = "int" } },
                        new Parameter { Name = "b", Type = new TypeAnnotation { Name = "int" } }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "int" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new IntegerLiteral { Value = "0" } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static int Multiply(int a, int b)", code);
    }

    [Fact]
    public void GenerateMethod_WithSelfParameter_GeneratesInstanceMethod()
    {
        // Arrange - Method with 'self' parameter should be instance method
        var classDef = new ClassDef
        {
            Name = "Counter",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "increment",
                    Decorators = ImmutableArray<Decorator>.Empty,
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null },
                        new Parameter { Name = "amount", Type = new TypeAnnotation { Name = "int" } }
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
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public void Increment(int amount)", code);
        Assert.DoesNotContain("static void Increment", code);
    }

    [Fact]
    public void GenerateClassDeclaration_WithSealedDecorator_GeneratesSealedClass()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "FinalImplementation",
            Decorators = new List<Decorator>
            {
                new Decorator { Name = "sealed" }
            }.ToImmutableArray(),
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public sealed class FinalImplementation", code);
    }

    [Fact]
    public void GenerateMethod_WithProtectedDecorator_GeneratesProtectedMethod()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "BaseClass",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "internal_helper",
                    Decorators = new List<Decorator>
                    {
                        new Decorator { Name = "protected" }
                    }.ToImmutableArray(),
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "int" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new IntegerLiteral { Value = "42" } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("protected int InternalHelper()", code);
    }

    [Fact]
    public void GenerateMethod_WithInternalDecorator_GeneratesInternalMethod()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "AssemblyHelper",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "assembly_method",
                    Decorators = new List<Decorator>
                    {
                        new Decorator { Name = "internal" }
                    }.ToImmutableArray(),
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" }
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
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("internal void AssemblyMethod()", code);
    }

    [Fact]
    public void GenerateMethod_WithVirtualDecorator_GeneratesVirtualMethod()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "BaseClass",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "overridable_method",
                    Decorators = new List<Decorator>
                    {
                        new Decorator { Name = "virtual" }
                    }.ToImmutableArray(),
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "str" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new StringLiteral { Value = "base" } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public virtual string OverridableMethod()", code);
    }

    [Fact]
    public void GenerateMethod_WithOverrideDecorator_GeneratesOverrideMethod()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "DerivedClass",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "overridden_method",
                    Decorators = new List<Decorator>
                    {
                        new Decorator { Name = "override" }
                    }.ToImmutableArray(),
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" }
                    }.ToImmutableArray(),
                    ReturnType = new TypeAnnotation { Name = "str" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new StringLiteral { Value = "derived" } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public override string OverriddenMethod()", code);
    }

    [Fact]
    public void GenerateClassDeclaration_WithStaticDecorator_GeneratesStaticClass()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "UtilityClass",
            Decorators = new List<Decorator>
            {
                new Decorator { Name = "static" }
            }.ToImmutableArray(),
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static class UtilityClass", code);
    }

    [Fact]
    public void GenerateMethod_WithMultipleDecorators_GeneratesCorrectModifiers()
    {
        // Arrange - protected virtual method
        var classDef = new ClassDef
        {
            Name = "BaseClass",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "template_method",
                    Decorators = new List<Decorator>
                    {
                        new Decorator { Name = "protected" },
                        new Decorator { Name = "virtual" }
                    }.ToImmutableArray(),
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" }
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
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("protected virtual void TemplateMethod()", code);
    }

    [Fact]
    public void GenerateClassDeclaration_WithPublicDecorator_GeneratesPublicClass()
    {
        // Arrange - explicit @public decorator
        var classDef = new ClassDef
        {
            Name = "PublicClass",
            Decorators = new List<Decorator>
            {
                new Decorator { Name = "public" }
            }.ToImmutableArray(),
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public class PublicClass", code);
    }

    [Fact]
    public void GenerateMethod_WithPublicDecorator_GeneratesPublicMethod()
    {
        // Arrange - explicit @public decorator
        var classDef = new ClassDef
        {
            Name = "TestClass",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "public_method",
                    Decorators = new List<Decorator>
                    {
                        new Decorator { Name = "public" }
                    }.ToImmutableArray(),
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" }
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
        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public void PublicMethod()", code);
    }

    #endregion

    #region Type Constraint Tests

    [Fact]
    public void EmitFunctionWithInterfaceConstraint()
    {
        var func = new FunctionDef
        {
            Name = "find_max",
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef
                {
                    Name = "T",
                    Constraints = new List<ConstraintClause>
                    {
                        new TypeConstraint { Type = new TypeAnnotation { Name = "IComparable" } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray(),
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "a", Type = new TypeAnnotation { Name = "T" } },
                new Parameter { Name = "b", Type = new TypeAnnotation { Name = "T" } }
            }.ToImmutableArray(),
            ReturnType = new TypeAnnotation { Name = "T" },
            Body = new List<Statement> { new ReturnStatement { Value = new Identifier { Name = "a" } } }.ToImmutableArray()
        };

        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        Assert.Contains("where T : IComparable", code);
    }

    [Fact]
    public void EmitFunctionWithClassConstraint()
    {
        var func = new FunctionDef
        {
            Name = "process",
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef
                {
                    Name = "T",
                    Constraints = new List<ConstraintClause> { new ClassConstraint() }.ToImmutableArray()
                }
            }.ToImmutableArray(),
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "item", Type = new TypeAnnotation { Name = "T" } }
            }.ToImmutableArray(),
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        Assert.Contains("where T : class", code);
    }

    [Fact]
    public void EmitFunctionWithStructConstraint()
    {
        var func = new FunctionDef
        {
            Name = "process",
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef
                {
                    Name = "T",
                    Constraints = new List<ConstraintClause> { new StructConstraint() }.ToImmutableArray()
                }
            }.ToImmutableArray(),
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "item", Type = new TypeAnnotation { Name = "T" } }
            }.ToImmutableArray(),
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        Assert.Contains("where T : struct", code);
    }

    [Fact]
    public void EmitFunctionWithNewConstraint()
    {
        var func = new FunctionDef
        {
            Name = "create",
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef
                {
                    Name = "T",
                    Constraints = new List<ConstraintClause> { new NewConstraint() }.ToImmutableArray()
                }
            }.ToImmutableArray(),
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = new TypeAnnotation { Name = "T" },
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        Assert.Contains("where T : new()", code);
    }

    [Fact]
    public void EmitFunctionWithMultipleConstraints()
    {
        var func = new FunctionDef
        {
            Name = "process",
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef
                {
                    Name = "T",
                    Constraints = new List<ConstraintClause>
                    {
                        new ClassConstraint(),
                        new TypeConstraint { Type = new TypeAnnotation { Name = "IFoo" } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray(),
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "item", Type = new TypeAnnotation { Name = "T" } }
            }.ToImmutableArray(),
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        var module = new Module { Body = new List<Statement> { func }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        Assert.Contains("where T : class, IFoo", code);
    }

    [Fact]
    public void EmitClassWithConstraint()
    {
        var classDef = new ClassDef
        {
            Name = "Container",
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef
                {
                    Name = "T",
                    Constraints = new List<ConstraintClause>
                    {
                        new TypeConstraint { Type = new TypeAnnotation { Name = "ISerializable" } }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray(),
            Body = new List<Statement> { new PassStatement() }.ToImmutableArray()
        };

        var module = new Module { Body = new List<Statement> { classDef }.ToImmutableArray() };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        Assert.Contains("where T : ISerializable", code);
    }

    #endregion
}
