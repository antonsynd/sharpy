using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

[Collection("Sequential")]
public class RoslynEmitterVariableRedefinitionTests
{
    private readonly CodeGenContext _context;
    private readonly RoslynEmitter _emitter;

    public RoslynEmitterVariableRedefinitionTests()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        _context = new CodeGenContext(symbolTable, builtins);
        _emitter = new RoslynEmitter(_context);
    }

    private string GenerateFunctionCode(FunctionDef func)
    {
        var method = typeof(RoslynEmitter).GetMethod("GenerateFunctionDeclaration",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method!.Invoke(_emitter, new object[] { func }) as MethodDeclarationSyntax;
        return result?.NormalizeWhitespace().ToFullString() ?? "";
    }

    #region Same Type Redefinition

    [Fact]
    public void GenerateFunction_RedefineSameTypeVariable_GeneratesVersionedNames()
    {
        var func = new FunctionDef
        {
            Name = "test_redefinition",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = null,
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Operator = AssignmentOperator.Assign,
                    Value = new IntegerLiteral { Value = "1" }
                },
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "auto" },
                    InitialValue = new IntegerLiteral { Value = "2" }
                },
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "auto" },
                    InitialValue = new IntegerLiteral { Value = "3" }
                },
                new ReturnStatement
                {
                    Value = new Identifier { Name = "x" }
                }
            }.ToImmutableArray(),
            Decorators = ImmutableArray<Decorator>.Empty
        };

        var result = GenerateFunctionCode(func);

        // Should generate:
        // var x = 1;
        // var x_1 = 2;
        // var x_2 = 3;
        // return x_2;
        Assert.Contains("var x = 1;", result);
        Assert.Contains("var x_1 = 2;", result);
        Assert.Contains("var x_2 = 3;", result);
        Assert.Contains("return x_2;", result);
    }

    [Fact]
    public void GenerateFunction_RedefineSameTypeWithExplicitType_GeneratesVersionedNames()
    {
        var func = new FunctionDef
        {
            Name = "test_redefinition",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = null,
            Body = new List<Statement>
            {
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = new IntegerLiteral { Value = "1" }
                },
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = new IntegerLiteral { Value = "2" }
                },
                new ReturnStatement
                {
                    Value = new Identifier { Name = "x" }
                }
            }.ToImmutableArray(),
            Decorators = ImmutableArray<Decorator>.Empty
        };

        var result = GenerateFunctionCode(func);

        // Should generate:
        // int x = 1;
        // int x_1 = 2;
        // return x_1;
        Assert.Contains("int x = 1;", result);
        Assert.Contains("int x_1 = 2;", result);
        Assert.Contains("return x_1;", result);
    }

    #endregion

    #region Different Type Redefinition

    [Fact]
    public void GenerateFunction_RedefineDifferentTypes_GeneratesVersionedNames()
    {
        var func = new FunctionDef
        {
            Name = "test_redefinition",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = null,
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Operator = AssignmentOperator.Assign,
                    Value = new IntegerLiteral { Value = "1" }
                },
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "auto" },
                    InitialValue = new StringLiteral { Value = "hello" }
                },
                new ReturnStatement
                {
                    Value = new Identifier { Name = "x" }
                }
            }.ToImmutableArray(),
            Decorators = ImmutableArray<Decorator>.Empty
        };

        var result = GenerateFunctionCode(func);

        // Should generate:
        // var x = 1;
        // var x_1 = "hello";
        // return x_1;
        Assert.Contains("var x = 1;", result);
        Assert.Contains("var x_1 = \"hello\";", result);
        Assert.Contains("return x_1;", result);
    }

    [Fact]
    public void GenerateFunction_RedefineDifferentTypesExplicit_GeneratesVersionedNames()
    {
        var func = new FunctionDef
        {
            Name = "test_redefinition",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = null,
            Body = new List<Statement>
            {
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = new IntegerLiteral { Value = "1" }
                },
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "string" },
                    InitialValue = new StringLiteral { Value = "hello" }
                },
                new ReturnStatement
                {
                    Value = new Identifier { Name = "x" }
                }
            }.ToImmutableArray(),
            Decorators = ImmutableArray<Decorator>.Empty
        };

        var result = GenerateFunctionCode(func);

        // Should generate:
        // int x = 1;
        // string x_1 = "hello";
        // return x_1;
        Assert.Contains("int x = 1;", result);
        Assert.Contains("string x_1 = \"hello\";", result);
        Assert.Contains("return x_1;", result);
    }

    #endregion

    #region Tuple Unpacking Redefinition

    [Fact]
    public void GenerateFunction_TupleUnpackingRedefinition_GeneratesVersionedNames()
    {
        var func = new FunctionDef
        {
            Name = "test_redefinition",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = null,
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new TupleLiteral
                    {
                        Elements = new List<Sharpy.Compiler.Parser.Ast.Expression>
                        {
                            new Identifier { Name = "x" },
                            new Identifier { Name = "y" }
                        }.ToImmutableArray()
                    },
                    Operator = AssignmentOperator.Assign,
                    Value = new TupleLiteral
                    {
                        Elements = new List<Sharpy.Compiler.Parser.Ast.Expression>
                        {
                            new IntegerLiteral { Value = "1" },
                            new IntegerLiteral { Value = "2" }
                        }.ToImmutableArray()
                    }
                },
                new Assignment
                {
                    Target = new TupleLiteral
                    {
                        Elements = new List<Sharpy.Compiler.Parser.Ast.Expression>
                        {
                            new Identifier { Name = "x" },
                            new Identifier { Name = "y" }
                        }.ToImmutableArray()
                    },
                    Operator = AssignmentOperator.Assign,
                    Value = new TupleLiteral
                    {
                        Elements = new List<Sharpy.Compiler.Parser.Ast.Expression>
                        {
                            new IntegerLiteral { Value = "3" },
                            new IntegerLiteral { Value = "4" }
                        }.ToImmutableArray()
                    }
                },
                new ReturnStatement
                {
                    Value = new Identifier { Name = "x" }
                }
            }.ToImmutableArray(),
            Decorators = ImmutableArray<Decorator>.Empty
        };

        var result = GenerateFunctionCode(func);

        // Should generate:
        // var(x, y) = (1, 2);
        // var(x_1, y_1) = (3, 4);
        // return x_1;
        Assert.Contains("var(x, y) = (1, 2);", result);
        Assert.Contains("var(x_1, y_1) = (3, 4);", result);
        Assert.Contains("return x_1;", result);
    }

    #endregion

    #region Augmented Assignment

    [Fact]
    public void GenerateFunction_AugmentedAssignment_UsesCurrentVersion()
    {
        var func = new FunctionDef
        {
            Name = "test_augmented",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = null,
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Operator = AssignmentOperator.Assign,
                    Value = new IntegerLiteral { Value = "1" }
                },
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Operator = AssignmentOperator.PlusAssign,
                    Value = new IntegerLiteral { Value = "1" }
                },
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "auto" },
                    InitialValue = new IntegerLiteral { Value = "10" }
                },
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Operator = AssignmentOperator.PlusAssign,
                    Value = new IntegerLiteral { Value = "5" }
                },
                new ReturnStatement
                {
                    Value = new Identifier { Name = "x" }
                }
            }.ToImmutableArray(),
            Decorators = ImmutableArray<Decorator>.Empty
        };

        var result = GenerateFunctionCode(func);

        // Should generate:
        // var x = 1;
        // x = x + 1;        // Uses current version (x)
        // var x_1 = 10;     // New declaration
        // x_1 = x_1 + 5;    // Uses current version (x_1)
        // return x_1;
        Assert.Contains("var x = 1;", result);
        Assert.Contains("x = x + 1;", result);
        Assert.Contains("var x_1 = 10;", result);
        Assert.Contains("x_1 = x_1 + 5;", result);
        Assert.Contains("return x_1;", result);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void GenerateFunction_ComplexRedefinitionScenario_GeneratesCorrectVersions()
    {
        var func = new FunctionDef
        {
            Name = "test_complex",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = null,
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Operator = AssignmentOperator.Assign,
                    Value = new IntegerLiteral { Value = "1" }
                },
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = new List<Sharpy.Compiler.Parser.Ast.Expression>
                        {
                            new Identifier { Name = "x" }
                        }.ToImmutableArray()
                    }
                },
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "auto" },
                    InitialValue = new IntegerLiteral { Value = "2" }
                },
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = new List<Sharpy.Compiler.Parser.Ast.Expression>
                        {
                            new Identifier { Name = "x" }
                        }.ToImmutableArray()
                    }
                },
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "auto" },
                    InitialValue = new StringLiteral { Value = "hello" }
                },
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = new List<Sharpy.Compiler.Parser.Ast.Expression>
                        {
                            new Identifier { Name = "x" }
                        }.ToImmutableArray()
                    }
                }
            }.ToImmutableArray(),
            Decorators = ImmutableArray<Decorator>.Empty
        };

        var result = GenerateFunctionCode(func);

        // Should generate:
        // var x = 1;
        // Sharpy.Builtins.Print(x);       // Uses x
        // var x_1 = 2;
        // Sharpy.Builtins.Print(x_1);     // Uses x_1
        // var x_2 = "hello";
        // Sharpy.Builtins.Print(x_2);     // Uses x_2
        Assert.Contains("var x = 1;", result);
        Assert.Contains("Sharpy.Builtins.Print(x);", result);
        Assert.Contains("var x_1 = 2;", result);
        Assert.Contains("Sharpy.Builtins.Print(x_1);", result);
        Assert.Contains("var x_2 = \"hello\";", result);
        Assert.Contains("Sharpy.Builtins.Print(x_2);", result);
    }

    [Fact]
    public void GenerateFunction_MultipleVariablesWithRedefinitions_GeneratesCorrectVersions()
    {
        var func = new FunctionDef
        {
            Name = "test_multiple",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = null,
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Operator = AssignmentOperator.Assign,
                    Value = new IntegerLiteral { Value = "1" }
                },
                new Assignment
                {
                    Target = new Identifier { Name = "y" },
                    Operator = AssignmentOperator.Assign,
                    Value = new IntegerLiteral { Value = "2" }
                },
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "auto" },
                    InitialValue = new IntegerLiteral { Value = "3" }
                },
                new VariableDeclaration
                {
                    Name = "y",
                    Type = new TypeAnnotation { Name = "auto" },
                    InitialValue = new IntegerLiteral { Value = "4" }
                },
                new ReturnStatement
                {
                    Value = new BinaryOp
                    {
                        Left = new Identifier { Name = "x" },
                        Operator = BinaryOperator.Add,
                        Right = new Identifier { Name = "y" }
                    }
                }
            }.ToImmutableArray(),
            Decorators = ImmutableArray<Decorator>.Empty
        };

        var result = GenerateFunctionCode(func);

        // Should generate:
        // var x = 1;
        // var y = 2;
        // var x_1 = 3;
        // var y_1 = 4;
        // return x_1 + y_1;
        Assert.Contains("var x = 1;", result);
        Assert.Contains("var y = 2;", result);
        Assert.Contains("var x_1 = 3;", result);
        Assert.Contains("var y_1 = 4;", result);
        Assert.Contains("return x_1 + y_1;", result);
    }

    #endregion

    #region Variable Name Collision Tests

    [Fact]
    public void GenerateFunction_UserDeclaredX1_NoCollision()
    {
        // In this scenario, user declares x, x_1, then redeclares x.
        // Since x_1 is mangled to x1 (camelCase removes underscore),
        // and the generated versioned name is x_1 (underscore preserved),
        // there is NO collision. This tests that the naming is correct.
        var func = new FunctionDef
        {
            Name = "test_collision",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = null,
            Body = new List<Statement>
            {
                // x = 1
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Operator = AssignmentOperator.Assign,
                    Value = new IntegerLiteral { Value = "1" }
                },
                // x_1 = "user" (mangled to x1)
                new Assignment
                {
                    Target = new Identifier { Name = "x_1" },
                    Operator = AssignmentOperator.Assign,
                    Value = new StringLiteral { Value = "user" }
                },
                // x = 2 (redeclaration - generates x_1, which is different from user's x1)
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "auto" },
                    InitialValue = new IntegerLiteral { Value = "2" }
                },
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = new List<Sharpy.Compiler.Parser.Ast.Expression>
                        {
                            new Identifier { Name = "x" }
                        }.ToImmutableArray()
                    }
                },
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = new List<Sharpy.Compiler.Parser.Ast.Expression>
                        {
                            new Identifier { Name = "x_1" }
                        }.ToImmutableArray()
                    }
                }
            }.ToImmutableArray(),
            Decorators = ImmutableArray<Decorator>.Empty
        };

        var result = GenerateFunctionCode(func);

        // x_1 (user's) is mangled to x1, x_1 (generated) stays as x_1 - no collision
        Assert.Contains("var x = 1;", result);
        Assert.Contains("var x1 = \"user\";", result);  // x_1 mangled to x1
        Assert.Contains("var x_1 = 2;", result);        // Generated version name
        Assert.Contains("Sharpy.Builtins.Print(x_1);", result);
        Assert.Contains("Sharpy.Builtins.Print(x1);", result);
    }

    [Fact]
    public void GenerateFunction_UserDeclaredSameAsVersioned_SkipsCollision()
    {
        // This tests the actual collision scenario where user declares x1 (without underscore),
        // which matches the pattern of a mangled versioned name if it existed.
        // The collision detection should still work for edge cases like backtick-escaped names.
        var func = new FunctionDef
        {
            Name = "test_no_collision",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = null,
            Body = new List<Statement>
            {
                // x = 1
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Operator = AssignmentOperator.Assign,
                    Value = new IntegerLiteral { Value = "1" }
                },
                // x1 = "user" (stays as x1, but generated versioned names use x_1 pattern)
                new Assignment
                {
                    Target = new Identifier { Name = "x1" },
                    Operator = AssignmentOperator.Assign,
                    Value = new StringLiteral { Value = "user" }
                },
                // x = 2 (redeclaration - generates x_1, different from x1)
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "auto" },
                    InitialValue = new IntegerLiteral { Value = "2" }
                },
                new ReturnStatement
                {
                    Value = new BinaryOp
                    {
                        Left = new Identifier { Name = "x" },
                        Operator = BinaryOperator.Add,
                        Right = new IntegerLiteral { Value = "10" }
                    }
                }
            }.ToImmutableArray(),
            Decorators = ImmutableArray<Decorator>.Empty
        };

        var result = GenerateFunctionCode(func);

        // x1 (user's) stays as x1, x_1 (generated) has underscore - no collision
        Assert.Contains("var x = 1;", result);
        Assert.Contains("var x1 = \"user\";", result);  // stays as x1
        Assert.Contains("var x_1 = 2;", result);        // Generated version has underscore
        Assert.Contains("return x_1 + 10;", result);
    }

    #endregion
}
