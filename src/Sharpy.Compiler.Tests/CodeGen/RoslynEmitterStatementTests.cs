using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

[Collection("Sequential")]
public class RoslynEmitterStatementTests
{
    private readonly CodeGenContext _context;
    private readonly RoslynEmitter _emitter;

    public RoslynEmitterStatementTests()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        _context = new CodeGenContext(symbolTable, builtins);
        _emitter = new RoslynEmitter(_context);
    }

    private string GenerateStatementCode(Statement stmt)
    {
        // Use reflection to call the private GenerateBodyStatement method
        var method = typeof(RoslynEmitter).GetMethod("GenerateBodyStatement",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method!.Invoke(_emitter, new object[] { stmt }) as StatementSyntax;
        return result?.NormalizeWhitespace().ToFullString() ?? "";
    }

    #region Simple Statements

    [Fact]
    public void GenerateStatement_PassStatement_GeneratesEmptyStatement()
    {
        var stmt = new PassStatement();

        var result = GenerateStatementCode(stmt);

        Assert.Equal(";", result);
    }

    [Fact]
    public void GenerateStatement_BreakStatement_GeneratesBreak()
    {
        var stmt = new Sharpy.Compiler.Parser.Ast.BreakStatement();

        var result = GenerateStatementCode(stmt);

        Assert.Equal("break;", result);
    }

    [Fact]
    public void GenerateStatement_ContinueStatement_GeneratesContinue()
    {
        var stmt = new Sharpy.Compiler.Parser.Ast.ContinueStatement();

        var result = GenerateStatementCode(stmt);

        Assert.Equal("continue;", result);
    }

    [Fact]
    public void GenerateStatement_AssertWithCondition_GeneratesDebugAssert()
    {
        var stmt = new AssertStatement
        {
            Test = new BooleanLiteral { Value = true }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Equal("System.Diagnostics.Debug.Assert(true);", result);
    }

    [Fact]
    public void GenerateStatement_AssertWithMessage_GeneratesDebugAssertWithMessage()
    {
        var stmt = new AssertStatement
        {
            Test = new BooleanLiteral { Value = true },
            Message = new StringLiteral { Value = "condition failed" }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Equal("System.Diagnostics.Debug.Assert(true, \"condition failed\");", result);
    }

    [Fact]
    public void GenerateStatement_RaiseWithException_GeneratesThrow()
    {
        var stmt = new RaiseStatement
        {
            Exception = new FunctionCall
            {
                Function = new Identifier { Name = "Exception" },
                Arguments = new List<Sharpy.Compiler.Parser.Ast.Expression>
                {
                    new StringLiteral { Value = "error" }
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Equal("throw Exception(\"error\");", result);
    }

    [Fact]
    public void GenerateStatement_RaiseWithoutException_GeneratesRethrow()
    {
        var stmt = new RaiseStatement();

        var result = GenerateStatementCode(stmt);

        Assert.Equal("throw;", result);
    }

    [Fact]
    public void GenerateStatement_VariableDeclarationWithInit_GeneratesLocalDeclaration()
    {
        var stmt = new VariableDeclaration
        {
            Name = "my_var",
            Type = new TypeAnnotation { Name = "int" },
            InitialValue = new IntegerLiteral { Value = "42" }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Equal("int myVar = 42;", result);
    }

    [Fact]
    public void GenerateStatement_VariableDeclarationWithoutInit_GeneratesLocalDeclaration()
    {
        var stmt = new VariableDeclaration
        {
            Name = "my_var",
            Type = new TypeAnnotation { Name = "int" }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Equal("int myVar;", result);
    }

    [Fact]
    public void GenerateStatement_ConstDeclaration_GeneratesConstLocalDeclaration()
    {
        var stmt = new VariableDeclaration
        {
            Name = "MAX_VALUE",
            Type = new TypeAnnotation { Name = "int" },
            InitialValue = new IntegerLiteral { Value = "100" },
            IsConst = true
        };

        var result = GenerateStatementCode(stmt);

        Assert.Contains("const int MAX_VALUE", result);
        Assert.Contains("= 100;", result);
    }

    [Fact]
    public void GenerateStatement_ListVarDeclaration_UsesTargetTypeForElements()
    {
        // Test that list[int] = [1, 2, 3] generates List<int>, not List<object>
        var stmt = new VariableDeclaration
        {
            Name = "numbers",
            Type = new TypeAnnotation
            {
                Name = "list",
                TypeArguments = new List<TypeAnnotation>
                {
                    new TypeAnnotation { Name = "int" }
                }
            },
            InitialValue = new ListLiteral
            {
                Elements = new List<Expression>
                {
                    new IntegerLiteral { Value = "1" },
                    new IntegerLiteral { Value = "2" },
                    new IntegerLiteral { Value = "3" }
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        // Should generate List<int>, not List<object>
        Assert.Contains("global::Sharpy.Core.List<int>", result);
        Assert.Contains("new global::Sharpy.Core.List<int>", result);
        Assert.DoesNotContain("List<object>", result);
    }

    [Fact]
    public void GenerateStatement_DictVarDeclaration_UsesTargetTypeForElements()
    {
        // Test that dict[str, int] = {"a": 1} generates Dict<string, int>
        var stmt = new VariableDeclaration
        {
            Name = "lookup",
            Type = new TypeAnnotation
            {
                Name = "dict",
                TypeArguments = new List<TypeAnnotation>
                {
                    new TypeAnnotation { Name = "str" },
                    new TypeAnnotation { Name = "int" }
                }
            },
            InitialValue = new DictLiteral
            {
                Entries = new List<DictEntry>
                {
                    new DictEntry
                    {
                        Key = new StringLiteral { Value = "a" },
                        Value = new IntegerLiteral { Value = "1" }
                    }
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        // Should generate Dict<string, int>
        Assert.Contains("global::Sharpy.Core.Dict<string, int>", result);
        Assert.Contains("new global::Sharpy.Core.Dict<string, int>", result);
        Assert.DoesNotContain("Dict<object", result);
    }

    [Fact]
    public void GenerateStatement_SetVarDeclaration_UsesTargetTypeForElements()
    {
        // Test that set[int] = {1, 2, 3} generates Set<int>
        var stmt = new VariableDeclaration
        {
            Name = "unique_nums",
            Type = new TypeAnnotation
            {
                Name = "set",
                TypeArguments = new List<TypeAnnotation>
                {
                    new TypeAnnotation { Name = "int" }
                }
            },
            InitialValue = new SetLiteral
            {
                Elements = new List<Expression>
                {
                    new IntegerLiteral { Value = "1" },
                    new IntegerLiteral { Value = "2" },
                    new IntegerLiteral { Value = "3" }
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        // Should generate Set<int>
        Assert.Contains("global::Sharpy.Core.Set<int>", result);
        Assert.Contains("new global::Sharpy.Core.Set<int>", result);
        Assert.DoesNotContain("Set<object>", result);
    }

    [Fact]
    public void GenerateStatement_ListVarWithoutType_InfersFromElements()
    {
        // Test that when no type annotation is provided, inference works
        var stmt = new VariableDeclaration
        {
            Name = "numbers",
            Type = null, // No type annotation - use inference
            InitialValue = new ListLiteral
            {
                Elements = new List<Expression>
                {
                    new IntegerLiteral { Value = "1" },
                    new IntegerLiteral { Value = "2" }
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        // Should infer List<int> from element types
        Assert.Contains("new global::Sharpy.Core.List<int>", result);
    }

    #endregion

    #region Assignment Statements

    [Fact]
    public void GenerateStatement_SimpleAssignment_GeneratesVariableDeclaration()
    {
        var stmt = new Assignment
        {
            Target = new Identifier { Name = "x" },
            Value = new IntegerLiteral { Value = "42" },
            Operator = AssignmentOperator.Assign
        };

        var result = GenerateStatementCode(stmt);

        Assert.Equal("var x = 42;", result);
    }

    [Fact]
    public void GenerateStatement_PlusAssignment_GeneratesAugmentedAssignment()
    {
        var stmt = new Assignment
        {
            Target = new Identifier { Name = "x" },
            Value = new IntegerLiteral { Value = "5" },
            Operator = AssignmentOperator.PlusAssign
        };

        var result = GenerateStatementCode(stmt);

        Assert.Equal("x = x + 5;", result);
    }

    [Fact]
    public void GenerateStatement_MinusAssignment_GeneratesAugmentedAssignment()
    {
        var stmt = new Assignment
        {
            Target = new Identifier { Name = "x" },
            Value = new IntegerLiteral { Value = "3" },
            Operator = AssignmentOperator.MinusAssign
        };

        var result = GenerateStatementCode(stmt);

        Assert.Equal("x = x - 3;", result);
    }

    [Fact]
    public void GenerateStatement_StarAssignment_GeneratesAugmentedAssignment()
    {
        var stmt = new Assignment
        {
            Target = new Identifier { Name = "x" },
            Value = new IntegerLiteral { Value = "2" },
            Operator = AssignmentOperator.StarAssign
        };

        var result = GenerateStatementCode(stmt);

        Assert.Equal("x = x * 2;", result);
    }

    [Fact]
    public void GenerateStatement_IndexAssignment_GeneratesElementAccessAssignment()
    {
        var stmt = new Assignment
        {
            Target = new IndexAccess
            {
                Object = new Identifier { Name = "arr" },
                Index = new IntegerLiteral { Value = "0" }
            },
            Value = new IntegerLiteral { Value = "42" },
            Operator = AssignmentOperator.Assign
        };

        var result = GenerateStatementCode(stmt);

        Assert.Equal("arr[0] = 42;", result);
    }

    [Fact]
    public void GenerateStatement_MemberAssignment_GeneratesMemberAccessAssignment()
    {
        var stmt = new Assignment
        {
            Target = new MemberAccess
            {
                Object = new Identifier { Name = "obj" },
                Member = "field"
            },
            Value = new IntegerLiteral { Value = "42" },
            Operator = AssignmentOperator.Assign
        };

        var result = GenerateStatementCode(stmt);

        Assert.Equal("obj.field = 42;", result);
    }

    #endregion

    #region Control Flow Statements

    [Fact]
    public void GenerateStatement_SimpleIf_GeneratesIfStatement()
    {
        var stmt = new IfStatement
        {
            Test = new BooleanLiteral { Value = true },
            ThenBody = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Value = new IntegerLiteral { Value = "1" },
                    Operator = AssignmentOperator.Assign
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Contains("if (true)", result);
        Assert.Contains("var x = 1;", result);
    }

    [Fact]
    public void GenerateStatement_IfElse_GeneratesIfElseStatement()
    {
        var stmt = new IfStatement
        {
            Test = new BooleanLiteral { Value = true },
            ThenBody = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Value = new IntegerLiteral { Value = "1" },
                    Operator = AssignmentOperator.Assign
                }
            },
            ElseBody = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Value = new IntegerLiteral { Value = "2" },
                    Operator = AssignmentOperator.Assign
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Contains("if (true)", result);
        Assert.Contains("= 1", result);
        Assert.Contains("else", result);
        Assert.Contains("= 2", result);
    }

    [Fact]
    public void GenerateStatement_IfElifElse_GeneratesIfElseIfElseStatement()
    {
        var stmt = new IfStatement
        {
            Test = new BinaryOp
            {
                Left = new Identifier { Name = "x" },
                Operator = BinaryOperator.GreaterThan,
                Right = new IntegerLiteral { Value = "10" }
            },
            ThenBody = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "y" },
                    Value = new IntegerLiteral { Value = "1" },
                    Operator = AssignmentOperator.Assign
                }
            },
            ElifClauses = new List<ElifClause>
            {
                new ElifClause
                {
                    Test = new BinaryOp
                    {
                        Left = new Identifier { Name = "x" },
                        Operator = BinaryOperator.GreaterThan,
                        Right = new IntegerLiteral { Value = "5" }
                    },
                    Body = new List<Statement>
                    {
                        new Assignment
                        {
                            Target = new Identifier { Name = "y" },
                            Value = new IntegerLiteral { Value = "2" },
                            Operator = AssignmentOperator.Assign
                        }
                    }
                }
            },
            ElseBody = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "y" },
                    Value = new IntegerLiteral { Value = "3" },
                    Operator = AssignmentOperator.Assign
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        // Check for the if structure
        Assert.Contains("if", result);
        Assert.Contains("> 10", result);
        Assert.Contains("= 1", result);
        // Check for else clause
        Assert.Contains("else", result);
        // Check for elif condition (should become nested if)
        Assert.Contains("> 5", result);
        Assert.Contains("= 2", result);
        // Check for final else
        Assert.Contains("= 3", result);
    }

    [Fact]
    public void GenerateStatement_WhileLoop_GeneratesWhileStatement()
    {
        var stmt = new WhileStatement
        {
            Test = new BinaryOp
            {
                Left = new Identifier { Name = "x" },
                Operator = BinaryOperator.LessThan,
                Right = new IntegerLiteral { Value = "10" }
            },
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Value = new IntegerLiteral { Value = "1" },
                    Operator = AssignmentOperator.PlusAssign
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Contains("while", result);
        Assert.Contains("< 10", result);
        Assert.Contains("+ 1", result);
    }

    [Fact]
    public void GenerateStatement_ForLoop_GeneratesForeachStatement()
    {
        var stmt = new ForStatement
        {
            Target = new Identifier { Name = "item" },
            Iterator = new Identifier { Name = "items" },
            Body = new List<Statement>
            {
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = new List<Sharpy.Compiler.Parser.Ast.Expression>
                        {
                            new Identifier { Name = "item" }
                        }
                    }
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Contains("foreach", result);
        Assert.Contains("var", result);
        Assert.Contains("in items", result);
        Assert.Contains("Sharpy.Core.Exports.Print", result);
    }

    [Fact]
    public void GenerateStatement_ForLoopWithBreak_GeneratesForeachWithBreak()
    {
        var stmt = new ForStatement
        {
            Target = new Identifier { Name = "item" },
            Iterator = new Identifier { Name = "items" },
            Body = new List<Statement>
            {
                new Sharpy.Compiler.Parser.Ast.BreakStatement()
            }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Contains("foreach (var item in items)", result);
        Assert.Contains("break;", result);
    }

    #endregion

    #region Exception Handling

    [Fact]
    public void GenerateStatement_TryExcept_GeneratesTryCatchStatement()
    {
        var stmt = new TryStatement
        {
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Value = new IntegerLiteral { Value = "1" },
                    Operator = AssignmentOperator.Assign
                }
            },
            Handlers = new List<ExceptHandler>
            {
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "Exception" },
                    Body = new List<Statement>
                    {
                        new PassStatement()
                    }
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Contains("try", result);
        Assert.Contains("var x = 1;", result);
        Assert.Contains("catch (Exception)", result);
    }

    [Fact]
    public void GenerateStatement_TryExceptWithName_GeneratesTryCatchWithVariable()
    {
        var stmt = new TryStatement
        {
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Value = new IntegerLiteral { Value = "1" },
                    Operator = AssignmentOperator.Assign
                }
            },
            Handlers = new List<ExceptHandler>
            {
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "Exception" },
                    Name = "e",
                    Body = new List<Statement>
                    {
                        new ExpressionStatement
                        {
                            Expression = new FunctionCall
                            {
                                Function = new Identifier { Name = "print" },
                                Arguments = new List<Sharpy.Compiler.Parser.Ast.Expression>
                                {
                                    new Identifier { Name = "e" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Contains("try", result);
        Assert.Contains("catch (Exception", result);
        Assert.Contains("Sharpy.Core.Exports.Print", result);
    }

    [Fact]
    public void GenerateStatement_TryFinally_GeneratesTryFinallyStatement()
    {
        var stmt = new TryStatement
        {
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Value = new IntegerLiteral { Value = "1" },
                    Operator = AssignmentOperator.Assign
                }
            },
            FinallyBody = new List<Statement>
            {
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "cleanup" },
                        Arguments = new List<Sharpy.Compiler.Parser.Ast.Expression>()
                    }
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Contains("try", result);
        Assert.Contains("var x = 1;", result);
        Assert.Contains("finally", result);
        Assert.Contains("Cleanup();", result);
    }

    [Fact]
    public void GenerateStatement_TryExceptFinally_GeneratesTryCatchFinallyStatement()
    {
        var stmt = new TryStatement
        {
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Value = new IntegerLiteral { Value = "1" },
                    Operator = AssignmentOperator.Assign
                }
            },
            Handlers = new List<ExceptHandler>
            {
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "Exception" },
                    Body = new List<Statement>
                    {
                        new PassStatement()
                    }
                }
            },
            FinallyBody = new List<Statement>
            {
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "cleanup" },
                        Arguments = new List<Sharpy.Compiler.Parser.Ast.Expression>()
                    }
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Contains("try", result);
        Assert.Contains("var x = 1;", result);
        Assert.Contains("catch (Exception)", result);
        Assert.Contains("finally", result);
        Assert.Contains("Cleanup();", result);
    }

    [Fact]
    public void GenerateStatement_TryMultipleExcept_GeneratesMultipleCatchClauses()
    {
        var stmt = new TryStatement
        {
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Value = new IntegerLiteral { Value = "1" },
                    Operator = AssignmentOperator.Assign
                }
            },
            Handlers = new List<ExceptHandler>
            {
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "ValueError" },
                    Body = new List<Statement>
                    {
                        new PassStatement()
                    }
                },
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "KeyError" },
                    Body = new List<Statement>
                    {
                        new PassStatement()
                    }
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        Assert.Contains("try", result);
        Assert.Contains("catch (ValueError)", result);
        Assert.Contains("catch (KeyError)", result);
    }

    [Fact]
    public void GenerateStatement_TryExceptElse_GeneratesFlagPattern()
    {
        var stmt = new TryStatement
        {
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Value = new IntegerLiteral { Value = "1" },
                    Operator = AssignmentOperator.Assign
                }
            },
            Handlers = new List<ExceptHandler>
            {
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "Exception" },
                    Body = new List<Statement>
                    {
                        new PassStatement()
                    }
                }
            },
            ElseBody = new List<Statement>
            {
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "success" },
                        Arguments = new List<Sharpy.Compiler.Parser.Ast.Expression>()
                    }
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        // Check for flag pattern: bool __trySucceeded_N = false;
        Assert.Contains("bool __trySucceeded_", result);
        Assert.Contains("= false", result);
        // Check flag is set to true at end of try block
        Assert.Contains("= true", result);
        // Check for try-catch
        Assert.Contains("try", result);
        Assert.Contains("catch (Exception)", result);
        // Check for else execution: if (__trySucceeded_N) { Success(); }
        Assert.Contains("if (__trySucceeded_", result);
        Assert.Contains("Success();", result);
    }

    [Fact]
    public void GenerateStatement_TryExceptElseFinally_GeneratesFlagPatternWithFinally()
    {
        var stmt = new TryStatement
        {
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "x" },
                    Value = new IntegerLiteral { Value = "1" },
                    Operator = AssignmentOperator.Assign
                }
            },
            Handlers = new List<ExceptHandler>
            {
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "Exception" },
                    Body = new List<Statement>
                    {
                        new PassStatement()
                    }
                }
            },
            ElseBody = new List<Statement>
            {
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "success" },
                        Arguments = new List<Sharpy.Compiler.Parser.Ast.Expression>()
                    }
                }
            },
            FinallyBody = new List<Statement>
            {
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "cleanup" },
                        Arguments = new List<Sharpy.Compiler.Parser.Ast.Expression>()
                    }
                }
            }
        };

        var result = GenerateStatementCode(stmt);

        // Check for flag pattern
        Assert.Contains("bool __trySucceeded_", result);
        // Check for try-catch-finally
        Assert.Contains("try", result);
        Assert.Contains("catch (Exception)", result);
        Assert.Contains("finally", result);
        Assert.Contains("Cleanup();", result);
        // Check for else execution after try-catch-finally
        Assert.Contains("if (__trySucceeded_", result);
        Assert.Contains("Success();", result);
    }

    #endregion
}
