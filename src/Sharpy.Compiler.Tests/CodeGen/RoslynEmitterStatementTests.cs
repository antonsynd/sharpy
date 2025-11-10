using Xunit;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.CodeGen;

public class RoslynEmitterStatementTests
{
    private readonly RoslynEmitter _emitter;
    private readonly CodeGenContext _context;

    public RoslynEmitterStatementTests()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        _context = new CodeGenContext(symbolTable, builtins);
        _emitter = new RoslynEmitter(_context);
    }

    private string GenerateStatementCode(Statement stmt)
    {
        var method = typeof(RoslynEmitter)
            .GetMethod("GenerateBodyStatement", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = method?.Invoke(_emitter, new object[] { stmt });
        return result?.ToString()?.Trim() ?? "";
    }

    #region Simple Statements

    [Fact]
    public void GenerateStatement_PassStatement_GeneratesEmptyStatement()
    {
        var stmt = new PassStatement();
        var code = GenerateStatementCode(stmt);
        
        Assert.Equal(";", code);
    }

    [Fact]
    public void GenerateStatement_BreakStatement_GeneratesBreak()
    {
        var stmt = new Sharpy.Compiler.Parser.Ast.BreakStatement();
        var code = GenerateStatementCode(stmt);
        
        Assert.Equal("break;", code);
    }

    [Fact]
    public void GenerateStatement_ContinueStatement_GeneratesContinue()
    {
        var stmt = new Sharpy.Compiler.Parser.Ast.ContinueStatement();
        var code = GenerateStatementCode(stmt);
        
        Assert.Equal("continue;", code);
    }

    [Fact]
    public void GenerateStatement_VariableDeclarationWithInit_GeneratesVarDecl()
    {
        var stmt = new VariableDeclaration
        {
            Name = "my_var",
            Type = new TypeAnnotation { Name = "int" },
            InitialValue = new IntegerLiteral { Value = "42" }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("int", code);
        Assert.Contains("myVar", code);
        Assert.Contains("=", code);
        Assert.Contains("42", code);
    }

    [Fact]
    public void GenerateStatement_ConstDeclaration_GeneratesConstModifier()
    {
        var stmt = new VariableDeclaration
        {
            Name = "MAX_SIZE",
            Type = new TypeAnnotation { Name = "int" },
            InitialValue = new IntegerLiteral { Value = "100" },
            IsConst = true
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("const", code);
        Assert.Contains("int", code);
        Assert.Contains("maxSize", code);
        Assert.Contains("=", code);
        Assert.Contains("100", code);
    }

    [Fact]
    public void GenerateStatement_VariableDeclarationNoInit_GeneratesDecl()
    {
        var stmt = new VariableDeclaration
        {
            Name = "my_var",
            Type = new TypeAnnotation { Name = "int" }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("int", code);
        Assert.Contains("myVar", code);
        Assert.DoesNotContain("=", code);
    }

    [Fact]
    public void GenerateStatement_AugmentedAssignment_GeneratesCompoundOp()
    {
        var stmt = new Assignment
        {
            Target = new Identifier { Name = "x" },
            Value = new IntegerLiteral { Value = "5" },
            Operator = AssignmentOperator.PlusAssign
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("+=", code);
        Assert.Contains("5", code);
    }

    [Fact]
    public void GenerateStatement_FloorDivideAssignment_GeneratesCastAndDivide()
    {
        var stmt = new Assignment
        {
            Target = new Identifier { Name = "x" },
            Value = new IntegerLiteral { Value = "3" },
            Operator = AssignmentOperator.DoubleSlashAssign
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("(int)", code);
        Assert.Contains("/", code);
        Assert.Contains("3", code);
    }

    [Fact]
    public void GenerateStatement_PowerAssignment_GeneratesMathPow()
    {
        var stmt = new Assignment
        {
            Target = new Identifier { Name = "x" },
            Value = new IntegerLiteral { Value = "2" },
            Operator = AssignmentOperator.PowerAssign
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("Math.Pow", code);
    }

    [Fact]
    public void GenerateStatement_AssertWithMessage_GeneratesDebugAssert()
    {
        var stmt = new AssertStatement
        {
            Test = new BinaryOp
            {
                Left = new Identifier { Name = "x" },
                Operator = BinaryOperator.GreaterThan,
                Right = new IntegerLiteral { Value = "0" }
            },
            Message = new StringLiteral { Value = "x must be positive" }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("System.Diagnostics.Debug.Assert", code);
        Assert.Contains(">", code);
        Assert.Contains("0", code);
        Assert.Contains("\"x must be positive\"", code);
    }

    [Fact]
    public void GenerateStatement_AssertNoMessage_GeneratesDebugAssert()
    {
        var stmt = new AssertStatement
        {
            Test = new BooleanLiteral { Value = true }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("System.Diagnostics.Debug.Assert", code);
        Assert.Contains("true", code);
    }

    [Fact]
    public void GenerateStatement_RaiseWithException_GeneratesThrow()
    {
        var stmt = new RaiseStatement
        {
            Exception = new FunctionCall
            {
                Function = new Identifier { Name = "ValueError" },
                Arguments = new List<Expression>
                {
                    new StringLiteral { Value = "Invalid value" }
                }
            }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("throw", code);
        Assert.Contains("Valueerror", code); // Name gets mangled to PascalCase
    }

    [Fact]
    public void GenerateStatement_RaiseNoException_GeneratesRethrow()
    {
        var stmt = new RaiseStatement();
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Equal("throw;", code);
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
                new PassStatement()
            }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("if", code);
        Assert.Contains("true", code);
        Assert.Contains("{", code);
        Assert.Contains("}", code);
    }

    [Fact]
    public void GenerateStatement_IfElse_GeneratesIfElse()
    {
        var stmt = new IfStatement
        {
            Test = new Identifier { Name = "condition" },
            ThenBody = new List<Statement>
            {
                new PassStatement()
            },
            ElseBody = new List<Statement>
            {
                new PassStatement()
            }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("if", code);
        Assert.Contains("condition", code);
        Assert.Contains("else", code);
    }

    [Fact]
    public void GenerateStatement_IfElifElse_GeneratesChain()
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
                new PassStatement()
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
                    Body = new List<Statement> { new PassStatement() }
                }
            },
            ElseBody = new List<Statement>
            {
                new PassStatement()
            }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("if", code);
        Assert.Contains(">", code);
        Assert.Contains("10", code);
        Assert.Contains("elseif", code);
        Assert.Contains("5", code);
        Assert.Contains("else", code);
    }

    [Fact]
    public void GenerateStatement_WhileLoop_GeneratesWhile()
    {
        var stmt = new WhileStatement
        {
            Test = new BinaryOp
            {
                Left = new Identifier { Name = "i" },
                Operator = BinaryOperator.LessThan,
                Right = new IntegerLiteral { Value = "10" }
            },
            Body = new List<Statement>
            {
                new Assignment
                {
                    Target = new Identifier { Name = "i" },
                    Value = new BinaryOp
                    {
                        Left = new Identifier { Name = "i" },
                        Operator = BinaryOperator.Add,
                        Right = new IntegerLiteral { Value = "1" }
                    },
                    Operator = AssignmentOperator.PlusAssign
                }
            }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("while", code);
        Assert.Contains("<", code);
        Assert.Contains("10", code);
        Assert.Contains("{", code);
        Assert.Contains("}", code);
    }

    [Fact]
    public void GenerateStatement_ForLoop_GeneratesForeach()
    {
        var stmt = new ForStatement
        {
            Target = new Identifier { Name = "item" },
            Iterator = new Identifier { Name = "items" },
            Body = new List<Statement>
            {
                new PassStatement()
            }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("foreach", code);
        Assert.Contains("var", code);
        Assert.Contains("item", code);
        Assert.Contains("in", code);
        Assert.Contains("items", code);
    }

    [Fact]
    public void GenerateStatement_ForLoopWithTupleUnpacking_GeneratesDestructuring()
    {
        var stmt = new ForStatement
        {
            Target = new TupleLiteral
            {
                Elements = new List<Expression>
                {
                    new Identifier { Name = "key" },
                    new Identifier { Name = "value" }
                }
            },
            Iterator = new Identifier { Name = "dict_items" },
            Body = new List<Statement>
            {
                new PassStatement()
            }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("foreach", code);
        Assert.Contains("_item", code);
        Assert.Contains("key", code);
        Assert.Contains("value", code);
    }

    #endregion

    #region Exception Handling

    [Fact]
    public void GenerateStatement_TryExcept_GeneratesTryCatch()
    {
        var stmt = new TryStatement
        {
            Body = new List<Statement>
            {
                new PassStatement()
            },
            Handlers = new List<ExceptHandler>
            {
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "Exception" },
                    Body = new List<Statement> { new PassStatement() }
                }
            }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("try", code);
        Assert.Contains("catch", code);
        Assert.Contains("Exception", code);
    }

    [Fact]
    public void GenerateStatement_TryExceptWithName_GeneratesCatchWithVar()
    {
        var stmt = new TryStatement
        {
            Body = new List<Statement>
            {
                new PassStatement()
            },
            Handlers = new List<ExceptHandler>
            {
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "Exception" },
                    Name = "ex",
                    Body = new List<Statement> { new PassStatement() }
                }
            }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("catch", code);
        Assert.Contains("Exception", code);
        Assert.Contains("ex", code);
    }

    [Fact]
    public void GenerateStatement_TryFinally_GeneratesTryFinally()
    {
        var stmt = new TryStatement
        {
            Body = new List<Statement>
            {
                new PassStatement()
            },
            FinallyBody = new List<Statement>
            {
                new PassStatement()
            }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("try", code);
        Assert.Contains("finally", code);
    }

    [Fact]
    public void GenerateStatement_TryExceptFinally_GeneratesComplete()
    {
        var stmt = new TryStatement
        {
            Body = new List<Statement>
            {
                new PassStatement()
            },
            Handlers = new List<ExceptHandler>
            {
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "Exception" },
                    Body = new List<Statement> { new PassStatement() }
                }
            },
            FinallyBody = new List<Statement>
            {
                new PassStatement()
            }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("try", code);
        Assert.Contains("catch", code);
        Assert.Contains("finally", code);
    }

    [Fact]
    public void GenerateStatement_MultipleCatchClauses_GeneratesAllCatches()
    {
        var stmt = new TryStatement
        {
            Body = new List<Statement>
            {
                new PassStatement()
            },
            Handlers = new List<ExceptHandler>
            {
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "ValueError" },
                    Body = new List<Statement> { new PassStatement() }
                },
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "KeyError" },
                    Body = new List<Statement> { new PassStatement() }
                }
            }
        };
        
        var code = GenerateStatementCode(stmt);
        
        Assert.Contains("catch", code);
        Assert.Contains("ValueError", code);
        Assert.Contains("KeyError", code);
    }

    #endregion
}
