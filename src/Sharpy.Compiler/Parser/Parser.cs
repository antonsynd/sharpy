using System.Text;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Parser;

/// <summary>
/// Parses Sharpy tokens into an Abstract Syntax Tree (AST).
/// Implements recursive descent parsing with operator precedence.
/// </summary>
public class Parser
{
    private readonly List<Token> _tokens;
    private int _position;
    private readonly ICompilerLogger _logger;

    public Parser(List<Token> tokens, ICompilerLogger? logger = null)
    {
        _tokens = tokens;
        _position = 0;
        _logger = logger ?? NullLogger.Instance;
        _logger.LogInfo($"Parser initialized, token count: {tokens.Count}");
    }

    private Token Current => _position < _tokens.Count ? _tokens[_position] : _tokens[^1];
    private Token Previous => _position > 0 ? _tokens[_position - 1] : _tokens[0];
    private Token Peek(int offset = 1) => _position + offset < _tokens.Count ? _tokens[_position + offset] : _tokens[^1];
    private bool IsAtEnd => Current.Type == TokenType.Eof;

    /// <summary>
    /// Parse the entire module
    /// </summary>
    public Module ParseModule()
    {
        _logger.LogInfo("Starting module parsing");
        var startTime = System.Diagnostics.Stopwatch.StartNew();

        var statements = new List<Statement>();
        string? docString = null;

        // Skip leading newlines
        SkipNewlines();

        // Check for module docstring
        if (Current.Type == TokenType.String)
        {
            docString = Current.Value;
            Advance();
            SkipNewlines();
        }

        while (!IsAtEnd)
        {
            SkipNewlines();
            if (IsAtEnd) break;

            var stmt = ParseStatement();
            statements.Add(stmt);
            SkipNewlines();
        }

        _logger.LogInfo($"Module parsing completed in {startTime.ElapsedMilliseconds}ms, {statements.Count} statements");

        return new Module
        {
            Body = statements,
            DocString = docString,
            LineStart = 1,
            ColumnStart = 1,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    #region Statement Parsing

    private Statement ParseStatement()
    {
        // Decorators (for functions, classes, structs)
        if (Current.Type == TokenType.At)
            return ParseDecoratedStatement();

        return Current.Type switch
        {
            TokenType.Def => ParseFunctionDef(),
            TokenType.Class => ParseClassDef(),
            TokenType.Struct => ParseStructDef(),
            TokenType.Interface => ParseInterfaceDef(),
            TokenType.Enum => ParseEnumDef(),
            TokenType.If => ParseIfStatement(),
            TokenType.While => ParseWhileStatement(),
            TokenType.For => ParseForStatement(),
            TokenType.Try => ParseTryStatement(),
            TokenType.Return => ParseReturnStatement(),
            TokenType.Raise => ParseRaiseStatement(),
            TokenType.Assert => ParseAssertStatement(),
            TokenType.Pass => ParsePassStatement(),
            TokenType.Break => ParseBreakStatement(),
            TokenType.Continue => ParseContinueStatement(),
            TokenType.Import => ParseImportStatement(),
            TokenType.From => ParseFromImportStatement(),
            TokenType.Const => ParseConstDeclaration(),
            _ => ParseSimpleStatement()
        };
    }

    private Statement ParseDecoratedStatement()
    {
        var decorators = new List<Decorator>();

        while (Current.Type == TokenType.At)
        {
            var decoratorStartLine = Current.Line;
            var decoratorStartColumn = Current.Column;
            Advance();  // Skip @
            if (Current.Type != TokenType.Identifier)
                throw new ParserError("Expected decorator name", Current.Line, Current.Column);

            var decoratorName = Current.Value;
            Advance();
            var decoratorEndLine = Peek(-1).Line;
            var decoratorEndColumn = Peek(-1).Column + Peek(-1).Value.Length;

            decorators.Add(new Decorator
            {
                Name = decoratorName,
                LineStart = decoratorStartLine,
                ColumnStart = decoratorStartColumn,
                LineEnd = decoratorEndLine,
                ColumnEnd = decoratorEndColumn
            });
            ExpectNewline();
        }

        // Parse the decorated definition
        Statement stmt = Current.Type switch
        {
            TokenType.Def => ParseFunctionDef(),
            TokenType.Class => ParseClassDef(),
            TokenType.Struct => ParseStructDef(),
            _ => throw new ParserError("Decorators can only be applied to functions, classes, or structs", Current.Line, Current.Column)
        };

        // Attach decorators
        return stmt switch
        {
            FunctionDef func => func with { Decorators = decorators },
            ClassDef cls => cls with { Decorators = decorators },
            StructDef str => str with { Decorators = decorators },
            _ => throw new ParserError("Unexpected decorated statement type", Current.Line, Current.Column)
        };
    }

    private Statement ParseSimpleStatement()
    {
        // Could be:
        // 1. Assignment (x = value, x += value)
        // 2. Variable declaration (x: int = value or x: int)
        // 3. Expression statement

        var expr = ParseExpression();

        // Check for tuple unpacking: x, y = ...
        // If we see a comma after the expression, it might be a tuple target for assignment
        if (Current.Type == TokenType.Comma)
        {
            var startLine = expr.LineStart;
            var startColumn = expr.ColumnStart;
            var elements = new List<Expression> { expr };

            // Parse remaining tuple elements
            while (Current.Type == TokenType.Comma)
            {
                Advance();
                elements.Add(ParseExpression());
            }

            // Now check if we have an assignment operator
            if (Current.Type >= TokenType.Assign && Current.Type <= TokenType.RightShiftAssign)
            {
                // This is a tuple unpacking assignment
                var tuple = new TupleLiteral
                {
                    Elements = elements,
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = Current.Line,
                    ColumnEnd = Current.Column
                };

                var op = TokenTypeToAssignmentOperator(Current.Type);
                Advance();
                var value = ParseExpression();
                ExpectStatementEnd();

                return new Assignment
                {
                    Target = tuple,
                    Value = value,
                    Operator = op,
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = value.LineEnd,
                    ColumnEnd = value.ColumnEnd
                };
            }

            // If not an assignment, this is an error (tuple expression statements not allowed)
            throw new ParserError("Tuple expression not allowed as a statement", Current.Line, Current.Column);
        }

        // Check for assignment operators
        if (Current.Type >= TokenType.Assign && Current.Type <= TokenType.RightShiftAssign)
        {
            var op = TokenTypeToAssignmentOperator(Current.Type);
            Advance();
            var value = ParseExpression();
            ExpectStatementEnd();

            return new Assignment
            {
                Target = expr,
                Value = value,
                Operator = op,
                LineStart = expr.LineStart,
                ColumnStart = expr.ColumnStart,
                LineEnd = value.LineEnd,
                ColumnEnd = value.ColumnEnd
            };
        }

        // Check for type annotation (variable declaration)
        if (Current.Type == TokenType.Colon)
        {
            if (expr is not Identifier id)
                throw new ParserError("Invalid type annotation target", Current.Line, Current.Column);

            Advance();  // Skip :
            var type = ParseTypeAnnotation();

            Expression? initialValue = null;
            if (Current.Type == TokenType.Assign)
            {
                Advance();
                initialValue = ParseExpression();
            }

            ExpectStatementEnd();

            return new VariableDeclaration
            {
                Name = id.Name,
                Type = type,
                InitialValue = initialValue,
                IsConst = false,
                LineStart = id.LineStart,
                ColumnStart = id.ColumnStart,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column
            };
        }

        ExpectStatementEnd();

        return new ExpressionStatement
        {
            Expression = expr,
            LineStart = expr.LineStart,
            ColumnStart = expr.ColumnStart,
            LineEnd = expr.LineEnd,
            ColumnEnd = expr.ColumnEnd
        };
    }

    private AssignmentOperator TokenTypeToAssignmentOperator(TokenType type) => type switch
    {
        TokenType.Assign => AssignmentOperator.Assign,
        TokenType.PlusAssign => AssignmentOperator.PlusAssign,
        TokenType.MinusAssign => AssignmentOperator.MinusAssign,
        TokenType.StarAssign => AssignmentOperator.StarAssign,
        TokenType.SlashAssign => AssignmentOperator.SlashAssign,
        TokenType.DoubleSlashAssign => AssignmentOperator.DoubleSlashAssign,
        TokenType.PercentAssign => AssignmentOperator.PercentAssign,
        TokenType.DoubleStarAssign => AssignmentOperator.PowerAssign,
        TokenType.AmpersandAssign => AssignmentOperator.AndAssign,
        TokenType.PipeAssign => AssignmentOperator.OrAssign,
        TokenType.CaretAssign => AssignmentOperator.XorAssign,
        TokenType.LeftShiftAssign => AssignmentOperator.LeftShiftAssign,
        TokenType.RightShiftAssign => AssignmentOperator.RightShiftAssign,
        _ => throw new ParserError($"Not an assignment operator: {type}", Current.Line, Current.Column)
    };

    private FunctionDef ParseFunctionDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Def);
        var name = ExpectIdentifier();
        Expect(TokenType.LeftParen);

        var parameters = ParseParameters();
        Expect(TokenType.RightParen);

        TypeAnnotation? returnType = null;
        if (Current.Type == TokenType.Arrow)
        {
            Advance();
            returnType = ParseTypeAnnotation();
        }

        Expect(TokenType.Colon);
        ExpectNewline();

        string? docString = null;
        Expect(TokenType.Indent);

        // Check for docstring
        if (Current.Type == TokenType.String)
        {
            docString = Current.Value;
            Advance();
            SkipNewlines();
        }

        var body = ParseBlock();
        Expect(TokenType.Dedent);

        return new FunctionDef
        {
            Name = name,
            Parameters = parameters,
            ReturnType = returnType,
            Body = body,
            DocString = docString,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private ClassDef ParseClassDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Class);
        var name = ExpectIdentifier();

        var typeParams = new List<string>();
        var baseClasses = new List<TypeAnnotation>();

        // Type parameters [T, U]
        if (Current.Type == TokenType.LeftBracket)
        {
            Advance();
            do
            {
                typeParams.Add(ExpectIdentifier());
                if (Current.Type == TokenType.Comma)
                    Advance();
                else
                    break;
            } while (true);
            Expect(TokenType.RightBracket);
        }

        // Base classes (ParentClass, Interface1, Interface2)
        if (Current.Type == TokenType.LeftParen)
        {
            Advance();
            if (Current.Type != TokenType.RightParen)
            {
                do
                {
                    baseClasses.Add(ParseTypeAnnotation());
                    if (Current.Type == TokenType.Comma)
                        Advance();
                    else
                        break;
                } while (true);
            }
            Expect(TokenType.RightParen);
        }

        Expect(TokenType.Colon);
        ExpectNewline();

        string? docString = null;
        Expect(TokenType.Indent);

        // Check for docstring
        if (Current.Type == TokenType.String)
        {
            docString = Current.Value;
            Advance();
            SkipNewlines();
        }

        var body = ParseBlock();
        Expect(TokenType.Dedent);

        return new ClassDef
        {
            Name = name,
            TypeParameters = typeParams,
            BaseClasses = baseClasses,
            Body = body,
            DocString = docString,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private StructDef ParseStructDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Struct);
        var name = ExpectIdentifier();

        var typeParams = new List<string>();
        var baseInterfaces = new List<TypeAnnotation>();

        // Type parameters [T, U]
        if (Current.Type == TokenType.LeftBracket)
        {
            Advance();
            do
            {
                typeParams.Add(ExpectIdentifier());
                if (Current.Type == TokenType.Comma)
                    Advance();
                else
                    break;
            } while (true);
            Expect(TokenType.RightBracket);
        }

        // Base interfaces (structs can only implement interfaces, no inheritance)
        if (Current.Type == TokenType.LeftParen)
        {
            Advance();
            if (Current.Type != TokenType.RightParen)
            {
                do
                {
                    baseInterfaces.Add(ParseTypeAnnotation());
                    if (Current.Type == TokenType.Comma)
                        Advance();
                    else
                        break;
                } while (true);
            }
            Expect(TokenType.RightParen);
        }

        Expect(TokenType.Colon);
        ExpectNewline();

        string? docString = null;
        Expect(TokenType.Indent);

        // Check for docstring
        if (Current.Type == TokenType.String)
        {
            docString = Current.Value;
            Advance();
            SkipNewlines();
        }

        var body = ParseBlock();
        Expect(TokenType.Dedent);

        return new StructDef
        {
            Name = name,
            TypeParameters = typeParams,
            BaseClasses = baseInterfaces,
            Body = body,
            DocString = docString,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private InterfaceDef ParseInterfaceDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Interface);
        var name = ExpectIdentifier();

        var typeParams = new List<string>();
        var baseInterfaces = new List<TypeAnnotation>();

        // Type parameters
        if (Current.Type == TokenType.LeftBracket)
        {
            Advance();
            do
            {
                typeParams.Add(ExpectIdentifier());
                if (Current.Type == TokenType.Comma)
                    Advance();
                else
                    break;
            } while (true);
            Expect(TokenType.RightBracket);
        }

        // Base interfaces
        if (Current.Type == TokenType.LeftParen)
        {
            Advance();
            if (Current.Type != TokenType.RightParen)
            {
                do
                {
                    baseInterfaces.Add(ParseTypeAnnotation());
                    if (Current.Type == TokenType.Comma)
                        Advance();
                    else
                        break;
                } while (true);
            }
            Expect(TokenType.RightParen);
        }

        Expect(TokenType.Colon);
        ExpectNewline();

        string? docString = null;
        Expect(TokenType.Indent);

        // Check for docstring
        if (Current.Type == TokenType.String)
        {
            docString = Current.Value;
            Advance();
            SkipNewlines();
        }

        var body = ParseBlock();
        Expect(TokenType.Dedent);

        return new InterfaceDef
        {
            Name = name,
            TypeParameters = typeParams,
            BaseInterfaces = baseInterfaces,
            Body = body,
            DocString = docString,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private EnumDef ParseEnumDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Enum);
        var name = ExpectIdentifier();
        Expect(TokenType.Colon);
        ExpectNewline();

        string? docString = null;
        Expect(TokenType.Indent);

        // Check for docstring
        if (Current.Type == TokenType.String)
        {
            docString = Current.Value;
            Advance();
            SkipNewlines();
        }

        var members = new List<EnumMember>();

        while (Current.Type != TokenType.Dedent && !IsAtEnd)
        {
            // Handle pass statement in empty enum
            if (Current.Type == TokenType.Pass)
            {
                Advance();
                ExpectNewline();
                SkipNewlines();
                continue;
            }

            var memberStartLine = Current.Line;
            var memberStartColumn = Current.Column;
            var memberName = ExpectIdentifier();
            Expression? value = null;

            if (Current.Type == TokenType.Assign)
            {
                Advance();
                value = ParseExpression();
            }

            var memberEndLine = Peek(-1).Line;
            var memberEndColumn = Peek(-1).Column + Peek(-1).Value.Length;

            members.Add(new EnumMember
            {
                Name = memberName,
                Value = value,
                LineStart = memberStartLine,
                ColumnStart = memberStartColumn,
                LineEnd = memberEndLine,
                ColumnEnd = memberEndColumn
            });
            ExpectNewline();
            SkipNewlines();
        }

        Expect(TokenType.Dedent);

        // Validate enum has at least one member
        if (members.Count == 0)
        {
            throw new ParserError($"Enum '{name}' must have at least one member", startLine, startColumn);
        }

        return new EnumDef
        {
            Name = name,
            Members = members,
            DocString = docString,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private VariableDeclaration ParseConstDeclaration()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Const);
        var name = ExpectIdentifier();
        Expect(TokenType.Colon);
        var type = ParseTypeAnnotation();
        Expect(TokenType.Assign);
        var value = ParseExpression();
        ExpectNewline();

        return new VariableDeclaration
        {
            Name = name,
            Type = type,
            InitialValue = value,
            IsConst = true,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private IfStatement ParseIfStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.If);
        var test = ParseExpression();
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var thenBody = ParseBlock();
        Expect(TokenType.Dedent);

        var elifClauses = new List<ElifClause>();
        var elseBody = new List<Statement>();

        // Elif clauses
        while (Current.Type == TokenType.Elif)
        {
            var elifStartLine = Current.Line;
            var elifStartColumn = Current.Column;
            Advance();
            var elifTest = ParseExpression();
            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            var elifBody = ParseBlock();
            Expect(TokenType.Dedent);
            var elifEndLine = Peek(-1).Line;
            var elifEndColumn = Peek(-1).Column + Peek(-1).Value.Length;

            elifClauses.Add(new ElifClause
            {
                Test = elifTest,
                Body = elifBody,
                LineStart = elifStartLine,
                ColumnStart = elifStartColumn,
                LineEnd = elifEndLine,
                ColumnEnd = elifEndColumn
            });
        }

        // Else clause
        if (Current.Type == TokenType.Else)
        {
            Advance();
            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            elseBody = ParseBlock();
            Expect(TokenType.Dedent);
        }

        return new IfStatement
        {
            Test = test,
            ThenBody = thenBody,
            ElifClauses = elifClauses,
            ElseBody = elseBody,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private WhileStatement ParseWhileStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.While);
        var test = ParseExpression();
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var body = ParseBlock();
        Expect(TokenType.Dedent);

        return new WhileStatement
        {
            Test = test,
            Body = body,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private ForStatement ParseForStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.For);

        // Parse target - this should be a simple identifier or tuple, not a full expression
        // We need to stop before consuming 'in' as a comparison operator
        var target = ParseForTarget();

        Expect(TokenType.In);
        var iterator = ParseExpression();
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var body = ParseBlock();
        Expect(TokenType.Dedent);

        return new ForStatement
        {
            Target = target,
            Iterator = iterator,
            Body = body,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private Expression ParseForTarget()
    {
        // For target can be:
        // - Simple identifier: for x in ...
        // - Tuple: for x, y in ...
        // We parse up to but not including the 'in' keyword

        var startLine = Current.Line;
        var startColumn = Current.Column;

        var first = ParsePrimary();

        // Check if it's a tuple (comma-separated)
        if (Current.Type == TokenType.Comma)
        {
            var elements = new List<Expression> { first };

            while (Current.Type == TokenType.Comma)
            {
                Advance();
                if (Current.Type == TokenType.In)
                    break;  // Trailing comma before 'in'
                elements.Add(ParsePrimary());
            }

            return new TupleLiteral
            {
                Elements = elements,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column
            };
        }

        return first;
    }

    /// <summary>
    /// Parse comprehension clauses: for x in iterable [if condition] [for y in iterable2] ...
    /// For now, only supporting single variable (no tuple unpacking in comprehensions)
    /// </summary>
    private List<ComprehensionClause> ParseComprehensionClauses()
    {
        var clauses = new List<ComprehensionClause>();

        while (true)
        {
            if (Current.Type == TokenType.For)
            {
                var startLine = Current.Line;
                var startColumn = Current.Column;
                Advance();

                // Parse target (single identifier for now)
                var target = ParseForTarget();

                Expect(TokenType.In);
                var iterator = ParseLogicalOr(); // Use lower precedence to avoid consuming too much

                clauses.Add(new ForClause
                {
                    Target = target,
                    Iterator = iterator,
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = Current.Line,
                    ColumnEnd = Current.Column
                });
            }
            else if (Current.Type == TokenType.If)
            {
                var startLine = Current.Line;
                var startColumn = Current.Column;
                Advance();

                var condition = ParseLogicalOr(); // Use lower precedence to avoid consuming too much

                clauses.Add(new IfClause
                {
                    Condition = condition,
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = Current.Line,
                    ColumnEnd = Current.Column
                });
            }
            else
            {
                break;
            }
        }

        return clauses;
    }

    private TryStatement ParseTryStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Try);
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var body = ParseBlock();
        Expect(TokenType.Dedent);

        var handlers = new List<ExceptHandler>();

        while (Current.Type == TokenType.Except)
        {
            var handlerStartLine = Current.Line;
            var handlerStartColumn = Current.Column;
            Advance();

            TypeAnnotation? exceptionType = null;
            string? name = null;

            // except ExceptionType as name:
            if (Current.Type != TokenType.Colon)
            {
                exceptionType = ParseTypeAnnotation();

                if (Current.Type == TokenType.As)
                {
                    Advance();
                    name = ExpectIdentifier();
                }
            }

            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            var handlerBody = ParseBlock();
            Expect(TokenType.Dedent);
            var handlerEndLine = Peek(-1).Line;
            var handlerEndColumn = Peek(-1).Column + Peek(-1).Value.Length;

            handlers.Add(new ExceptHandler
            {
                ExceptionType = exceptionType,
                Name = name,
                Body = handlerBody,
                LineStart = handlerStartLine,
                ColumnStart = handlerStartColumn,
                LineEnd = handlerEndLine,
                ColumnEnd = handlerEndColumn
            });
        }

        var finallyBody = new List<Statement>();
        if (Current.Type == TokenType.Finally)
        {
            Advance();
            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            finallyBody = ParseBlock();
            Expect(TokenType.Dedent);
        }

        return new TryStatement
        {
            Body = body,
            Handlers = handlers,
            FinallyBody = finallyBody,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private ReturnStatement ParseReturnStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Return);

        Expression? value = null;
        if (Current.Type != TokenType.Newline && Current.Type != TokenType.Dedent && !IsAtEnd)
            value = ParseExpression();

        ExpectStatementEnd();

        return new ReturnStatement
        {
            Value = value,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private RaiseStatement ParseRaiseStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Raise);

        Expression? exception = null;
        Expression? cause = null;

        if (Current.Type != TokenType.Newline && !IsAtEnd)
        {
            exception = ParseExpression();

            // raise ... from cause
            if (Current.Type == TokenType.From)
            {
                Advance();
                cause = ParseExpression();
            }
        }

        ExpectNewline();

        return new RaiseStatement
        {
            Exception = exception,
            Cause = cause,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private AssertStatement ParseAssertStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Assert);
        var test = ParseExpression();

        Expression? message = null;
        if (Current.Type == TokenType.Comma)
        {
            Advance();
            message = ParseExpression();
        }

        ExpectNewline();

        return new AssertStatement
        {
            Test = test,
            Message = message,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private PassStatement ParsePassStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Pass);
        ExpectStatementEnd();

        return new PassStatement
        {
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private BreakStatement ParseBreakStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Break);
        ExpectStatementEnd();

        return new BreakStatement
        {
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private ContinueStatement ParseContinueStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Continue);
        ExpectStatementEnd();

        return new ContinueStatement
        {
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private ImportStatement ParseImportStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.Import);

        var names = new List<ImportAlias>();

        do
        {
            var aliasStartLine = Current.Line;
            var aliasStartColumn = Current.Column;
            var name = ParseDottedName();
            string? asName = null;

            if (Current.Type == TokenType.As)
            {
                Advance();
                asName = ExpectIdentifier();
            }

            var aliasEndLine = Peek(-1).Line;
            var aliasEndColumn = Peek(-1).Column + Peek(-1).Value.Length;

            names.Add(new ImportAlias
            {
                Name = name,
                AsName = asName,
                LineStart = aliasStartLine,
                ColumnStart = aliasStartColumn,
                LineEnd = aliasEndLine,
                ColumnEnd = aliasEndColumn
            });

            if (Current.Type == TokenType.Comma)
                Advance();
            else
                break;
        } while (true);

        ExpectNewline();

        return new ImportStatement
        {
            Names = names,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private FromImportStatement ParseFromImportStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        Expect(TokenType.From);
        var module = ParseDottedName();
        Expect(TokenType.Import);

        var names = new List<ImportAlias>();
        var importAll = false;

        if (Current.Type == TokenType.Star)
        {
            Advance();
            importAll = true;
        }
        else
        {
            do
            {
                var aliasStartLine = Current.Line;
                var aliasStartColumn = Current.Column;
                var name = ExpectIdentifier();
                string? asName = null;

                if (Current.Type == TokenType.As)
                {
                    Advance();
                    asName = ExpectIdentifier();
                }

                var aliasEndLine = Peek(-1).Line;
                var aliasEndColumn = Peek(-1).Column + Peek(-1).Value.Length;

                names.Add(new ImportAlias
                {
                    Name = name,
                    AsName = asName,
                    LineStart = aliasStartLine,
                    ColumnStart = aliasStartColumn,
                    LineEnd = aliasEndLine,
                    ColumnEnd = aliasEndColumn
                });

                if (Current.Type == TokenType.Comma)
                    Advance();
                else
                    break;
            } while (true);
        }

        ExpectNewline();

        return new FromImportStatement
        {
            Module = module,
            Names = names,
            ImportAll = importAll,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    private string ParseDottedName()
    {
        var parts = new List<string> { ExpectIdentifier() };

        while (Current.Type == TokenType.Dot)
        {
            Advance();
            parts.Add(ExpectIdentifier());
        }

        return string.Join(".", parts);
    }

    private List<Statement> ParseBlock()
    {
        var statements = new List<Statement>();

        while (Current.Type != TokenType.Dedent && !IsAtEnd)
        {
            SkipNewlines();
            if (Current.Type == TokenType.Dedent || IsAtEnd)
                break;

            statements.Add(ParseStatement());
            SkipNewlines();
        }

        return statements;
    }

    private List<Parameter> ParseParameters()
    {
        var parameters = new List<Parameter>();

        if (Current.Type == TokenType.RightParen)
            return parameters;

        do
        {
            var startLine = Current.Line;
            var startColumn = Current.Column;

            var name = ExpectIdentifier();
            TypeAnnotation? type = null;
            Expression? defaultValue = null;

            if (Current.Type == TokenType.Colon)
            {
                Advance();
                type = ParseTypeAnnotation();
            }

            if (Current.Type == TokenType.Assign)
            {
                Advance();
                defaultValue = ParseExpression();
            }

            var endLine = Peek(-1).Line;
            var endColumn = Peek(-1).Column + Peek(-1).Value.Length;

            parameters.Add(new Parameter
            {
                Name = name,
                Type = type,
                DefaultValue = defaultValue,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = endLine,
                ColumnEnd = endColumn
            });

            if (Current.Type == TokenType.Comma)
                Advance();
            else
                break;
        } while (true);

        return parameters;
    }

    #endregion

    #region Expression Parsing (Precedence Climbing)

    private Expression ParseExpression() => ParseConditionalExpression();

    private Expression ParseConditionalExpression()
    {
        // expr if test else expr
        var expr = ParseNullCoalesce();

        if (Current.Type == TokenType.If)
        {
            Advance();
            var test = ParseNullCoalesce();
            Expect(TokenType.Else);
            var elseValue = ParseConditionalExpression();

            return new ConditionalExpression
            {
                Test = test,
                ThenValue = expr,
                ElseValue = elseValue,
                LineStart = expr.LineStart,
                ColumnStart = expr.ColumnStart,
                LineEnd = elseValue.LineEnd,
                ColumnEnd = elseValue.ColumnEnd
            };
        }

        return expr;
    }

    private Expression ParseNullCoalesce()
    {
        var left = ParseLogicalOr();

        while (Current.Type == TokenType.NullCoalesce)
        {
            Advance();
            var right = ParseLogicalOr();

            left = new BinaryOp
            {
                Operator = BinaryOperator.NullCoalesce,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd
            };
        }

        return left;
    }

    private Expression ParseLogicalOr()
    {
        var left = ParseLogicalAnd();

        while (Current.Type == TokenType.Or)
        {
            Advance();
            var right = ParseLogicalAnd();

            left = new BinaryOp
            {
                Operator = BinaryOperator.Or,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd
            };
        }

        return left;
    }

    private Expression ParseLogicalAnd()
    {
        var left = ParseLogicalNot();

        while (Current.Type == TokenType.And)
        {
            Advance();
            var right = ParseLogicalNot();

            left = new BinaryOp
            {
                Operator = BinaryOperator.And,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd
            };
        }

        return left;
    }

    private Expression ParseLogicalNot()
    {
        if (Current.Type == TokenType.Not)
        {
            var startLine = Current.Line;
            var startColumn = Current.Column;
            Advance();
            var operand = ParseLogicalNot();

            return new UnaryOp
            {
                Operator = UnaryOperator.Not,
                Operand = operand,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = operand.LineEnd,
                ColumnEnd = operand.ColumnEnd
            };
        }

        return ParseComparison();
    }

    private Expression ParseComparison()
    {
        var left = ParseBitwiseOr();

        // Special case: "is" followed by a type name should be parsed as TypeCheck
        if (Current.Type == TokenType.Is && Peek(1).Type == TokenType.Identifier)
        {
            var nextTokenValue = Peek(1).Value;
            // Check if it's a type name (starts with uppercase or is a known primitive type)
            if (IsTypeName(nextTokenValue))
            {
                var startLine = left.LineStart;
                var startColumn = left.ColumnStart;
                Advance(); // skip 'is'
                var typeStartLine = Current.Line;
                var typeStartColumn = Current.Column;
                var typeAnnotation = ParseTypeAnnotation();
                var endLine = Previous.Line;
                var endColumn = Previous.Column;
                return new TypeCheck
                {
                    Value = left,
                    CheckType = typeAnnotation,
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = endLine,
                    ColumnEnd = endColumn
                };
            }
        }

        // Check for comparison chain (a < b < c)
        var operators = new List<ComparisonOperator>();
        var operands = new List<Expression> { left };

        while (IsComparisonOperator(Current.Type))
        {
            var op = Current.Type;
            Advance();

            // Handle multi-token operators: "is not" and "not in"
            if (op == TokenType.Is && Current.Type == TokenType.Not)
            {
                Advance();
                operators.Add(ComparisonOperator.IsNot);
            }
            else if (op == TokenType.Not && Current.Type == TokenType.In)
            {
                Advance();
                operators.Add(ComparisonOperator.NotIn);
            }
            else
            {
                operators.Add(TokenTypeToComparisonOperator(op));
            }

            operands.Add(ParseBitwiseOr());
        }

        if (operators.Count == 0)
            return left;

        if (operators.Count == 1)
        {
            // Single comparison - use BinaryOp
            return new BinaryOp
            {
                Operator = ComparisonOperatorToBinary(operators[0]),
                Left = operands[0],
                Right = operands[1],
                LineStart = operands[0].LineStart,
                ColumnStart = operands[0].ColumnStart,
                LineEnd = operands[1].LineEnd,
                ColumnEnd = operands[1].ColumnEnd
            };
        }

        // Comparison chain
        return new ComparisonChain
        {
            Operands = operands,
            Operators = operators,
            LineStart = operands[0].LineStart,
            ColumnStart = operands[0].ColumnStart,
            LineEnd = operands[^1].LineEnd,
            ColumnEnd = operands[^1].ColumnEnd
        };
    }

    private bool IsComparisonOperator(TokenType type) => type switch
    {
        TokenType.Equal or TokenType.NotEqual or
        TokenType.Less or TokenType.LessEqual or
        TokenType.Greater or TokenType.GreaterEqual or
        TokenType.In or TokenType.Is or TokenType.Not => true,
        _ => false
    };

    private ComparisonOperator TokenTypeToComparisonOperator(TokenType type) => type switch
    {
        TokenType.Equal => ComparisonOperator.Equal,
        TokenType.NotEqual => ComparisonOperator.NotEqual,
        TokenType.Less => ComparisonOperator.LessThan,
        TokenType.LessEqual => ComparisonOperator.LessThanOrEqual,
        TokenType.Greater => ComparisonOperator.GreaterThan,
        TokenType.GreaterEqual => ComparisonOperator.GreaterThanOrEqual,
        TokenType.In => ComparisonOperator.In,
        TokenType.Is => ComparisonOperator.Is,
        _ => throw new ParserError($"Not a comparison operator: {type}", Current.Line, Current.Column)
    };

    private BinaryOperator ComparisonOperatorToBinary(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equal => BinaryOperator.Equal,
        ComparisonOperator.NotEqual => BinaryOperator.NotEqual,
        ComparisonOperator.LessThan => BinaryOperator.LessThan,
        ComparisonOperator.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
        ComparisonOperator.GreaterThan => BinaryOperator.GreaterThan,
        ComparisonOperator.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
        ComparisonOperator.In => BinaryOperator.In,
        ComparisonOperator.NotIn => BinaryOperator.NotIn,
        ComparisonOperator.Is => BinaryOperator.Is,
        ComparisonOperator.IsNot => BinaryOperator.IsNot,
        _ => throw new ParserError($"Cannot convert comparison operator to binary: {op}", Current.Line, Current.Column)
    };

    private Expression ParseBitwiseOr()
    {
        var left = ParseBitwiseXor();

        while (Current.Type == TokenType.Pipe)
        {
            Advance();
            var right = ParseBitwiseXor();

            left = new BinaryOp
            {
                Operator = BinaryOperator.BitwiseOr,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd
            };
        }

        return left;
    }

    private Expression ParseBitwiseXor()
    {
        var left = ParseBitwiseAnd();

        while (Current.Type == TokenType.Caret)
        {
            Advance();
            var right = ParseBitwiseAnd();

            left = new BinaryOp
            {
                Operator = BinaryOperator.BitwiseXor,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd
            };
        }

        return left;
    }

    private Expression ParseBitwiseAnd()
    {
        var left = ParseShift();

        while (Current.Type == TokenType.Ampersand)
        {
            Advance();
            var right = ParseShift();

            left = new BinaryOp
            {
                Operator = BinaryOperator.BitwiseAnd,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd
            };
        }

        return left;
    }

    private Expression ParseShift()
    {
        var left = ParseAdditive();

        while (Current.Type == TokenType.LeftShift || Current.Type == TokenType.RightShift)
        {
            var op = Current.Type == TokenType.LeftShift ? BinaryOperator.LeftShift : BinaryOperator.RightShift;
            Advance();
            var right = ParseAdditive();

            left = new BinaryOp
            {
                Operator = op,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd
            };
        }

        return left;
    }

    private Expression ParseAdditive()
    {
        var left = ParseMultiplicative();

        while (Current.Type == TokenType.Plus || Current.Type == TokenType.Minus)
        {
            var op = Current.Type == TokenType.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
            Advance();
            var right = ParseMultiplicative();

            left = new BinaryOp
            {
                Operator = op,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd
            };
        }

        return left;
    }

    private Expression ParseMultiplicative()
    {
        var left = ParseUnary();

        while (Current.Type == TokenType.Star || Current.Type == TokenType.Slash ||
               Current.Type == TokenType.DoubleSlash || Current.Type == TokenType.Percent)
        {
            var op = Current.Type switch
            {
                TokenType.Star => BinaryOperator.Multiply,
                TokenType.Slash => BinaryOperator.Divide,
                TokenType.DoubleSlash => BinaryOperator.FloorDivide,
                TokenType.Percent => BinaryOperator.Modulo,
                _ => throw new ParserError("Unexpected token", Current.Line, Current.Column)
            };
            Advance();
            var right = ParseUnary();

            left = new BinaryOp
            {
                Operator = op,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd
            };
        }

        return left;
    }

    private Expression ParseUnary()
    {
        if (Current.Type == TokenType.Plus || Current.Type == TokenType.Minus || Current.Type == TokenType.Tilde)
        {
            var startLine = Current.Line;
            var startColumn = Current.Column;
            var op = Current.Type switch
            {
                TokenType.Plus => UnaryOperator.Plus,
                TokenType.Minus => UnaryOperator.Minus,
                TokenType.Tilde => UnaryOperator.BitwiseNot,
                _ => throw new ParserError("Unexpected token", Current.Line, Current.Column)
            };
            Advance();
            var operand = ParseUnary();

            return new UnaryOp
            {
                Operator = op,
                Operand = operand,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = operand.LineEnd,
                ColumnEnd = operand.ColumnEnd
            };
        }

        return ParsePower();
    }

    private Expression ParsePower()
    {
        var left = ParsePostfix();

        if (Current.Type == TokenType.DoubleStar)
        {
            Advance();
            var right = ParseUnary();  // Right-associative

            return new BinaryOp
            {
                Operator = BinaryOperator.Power,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd
            };
        }

        return left;
    }

    private Expression ParsePostfix()
    {
        var expr = ParsePrimary();

        while (true)
        {
            if (Current.Type == TokenType.Dot || Current.Type == TokenType.NullConditional)
            {
                var isNullConditional = Current.Type == TokenType.NullConditional;
                Advance();

                var member = ExpectIdentifier();

                expr = new MemberAccess
                {
                    Object = expr,
                    Member = member,
                    IsNullConditional = isNullConditional,
                    LineStart = expr.LineStart,
                    ColumnStart = expr.ColumnStart,
                    LineEnd = Current.Line,
                    ColumnEnd = Current.Column
                };
            }
            else if (Current.Type == TokenType.LeftBracket)
            {
                Advance();
                var index = ParseSliceOrIndex();
                Expect(TokenType.RightBracket);

                if (index is IndexAccess ia)
                    expr = ia with { Object = expr };
                else if (index is SliceAccess sa)
                    expr = sa with { Object = expr };
            }
            else if (Current.Type == TokenType.LeftParen)
            {
                Advance();
                var args = new List<Expression>();
                var kwargs = new List<KeywordArgument>();

                if (Current.Type != TokenType.RightParen)
                {
                    do
                    {
                        // Check for keyword argument
                        if (Current.Type == TokenType.Identifier && Peek().Type == TokenType.Assign)
                        {
                            var kwargStartLine = Current.Line;
                            var kwargStartColumn = Current.Column;
                            var name = Current.Value;
                            Advance();  // Skip name
                            Advance();  // Skip =
                            var value = ParseExpression();
                            var kwargEndLine = Peek(-1).Line;
                            var kwargEndColumn = Peek(-1).Column + Peek(-1).Value.Length;

                            kwargs.Add(new KeywordArgument
                            {
                                Name = name,
                                Value = value,
                                LineStart = kwargStartLine,
                                ColumnStart = kwargStartColumn,
                                LineEnd = kwargEndLine,
                                ColumnEnd = kwargEndColumn
                            });
                        }
                        else
                        {
                            args.Add(ParseExpression());
                        }

                        if (Current.Type == TokenType.Comma)
                            Advance();
                        else
                            break;
                    } while (true);
                }

                Expect(TokenType.RightParen);

                expr = new FunctionCall
                {
                    Function = expr,
                    Arguments = args,
                    KeywordArguments = kwargs,
                    LineStart = expr.LineStart,
                    ColumnStart = expr.ColumnStart,
                    LineEnd = Current.Line,
                    ColumnEnd = Current.Column
                };
            }
            else if (Current.Type == TokenType.As)
            {
                // Type cast
                Advance();
                var targetType = ParseTypeAnnotation();

                expr = new TypeCast
                {
                    Value = expr,
                    TargetType = targetType,
                    LineStart = expr.LineStart,
                    ColumnStart = expr.ColumnStart,
                    LineEnd = Current.Line,
                    ColumnEnd = Current.Column
                };
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private Expression ParseSliceOrIndex()
    {
        Expression? start = null;
        Expression? stop = null;
        Expression? step = null;

        var isSlice = false;

        // [start:stop:step] or [index]
        if (Current.Type != TokenType.Colon)
            start = ParseExpression();

        if (Current.Type == TokenType.Colon)
        {
            isSlice = true;
            Advance();

            if (Current.Type != TokenType.Colon && Current.Type != TokenType.RightBracket)
                stop = ParseExpression();

            if (Current.Type == TokenType.Colon)
            {
                Advance();
                if (Current.Type != TokenType.RightBracket)
                    step = ParseExpression();
            }
        }

        if (isSlice)
        {
            return new SliceAccess
            {
                Object = null!,  // Will be filled in by caller
                Start = start,
                Stop = stop,
                Step = step,
                LineStart = Current.Line,
                ColumnStart = Current.Column,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column
            };
        }
        else
        {
            return new IndexAccess
            {
                Object = null!,  // Will be filled in by caller
                Index = start!,
                LineStart = Current.Line,
                ColumnStart = Current.Column,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column
            };
        }
    }

    private Expression ParsePrimary()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        switch (Current.Type)
        {
            case TokenType.Integer:
                {
                    var tokenValue = Current.Value;
                    Advance();

                    // Extract suffix if present (L, U, UL, etc.)
                    string value = tokenValue;
                    string? suffix = null;

                    if (tokenValue.Length > 0 && char.IsLetter(tokenValue[tokenValue.Length - 1]))
                    {
                        // Check for two-letter suffix
                        if (tokenValue.Length > 1 && char.IsLetter(tokenValue[tokenValue.Length - 2]))
                        {
                            suffix = tokenValue.Substring(tokenValue.Length - 2);
                            value = tokenValue.Substring(0, tokenValue.Length - 2);
                        }
                        else
                        {
                            suffix = tokenValue.Substring(tokenValue.Length - 1);
                            value = tokenValue.Substring(0, tokenValue.Length - 1);
                        }
                    }

                    return new IntegerLiteral { Value = value, Suffix = suffix, LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                }

            case TokenType.Float:
                {
                    var tokenValue = Current.Value;
                    Advance();

                    // Extract suffix if present (f, F, d, D, m, M)
                    string value = tokenValue;
                    string? suffix = null;

                    if (tokenValue.Length > 0 && char.IsLetter(tokenValue[tokenValue.Length - 1]))
                    {
                        suffix = tokenValue.Substring(tokenValue.Length - 1);
                        value = tokenValue.Substring(0, tokenValue.Length - 1);
                    }

                    return new FloatLiteral { Value = value, Suffix = suffix, LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                }

            case TokenType.String:
                {
                    var value = Current.Value;
                    Advance();
                    return new StringLiteral { Value = value, IsRaw = false, LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                }

            case TokenType.RawString:
                {
                    var value = Current.Value;
                    Advance();
                    return new StringLiteral { Value = value, IsRaw = true, LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                }

            case TokenType.FString:
                {
                    var value = Current.Value;
                    var endLine = Current.Line;
                    var endColumn = Current.Column;
                    Advance();
                    var parts = ParseFStringParts(value);
                    return new FStringLiteral { Parts = parts, LineStart = startLine, ColumnStart = startColumn, LineEnd = endLine, ColumnEnd = endColumn };
                }

            case TokenType.True:
                Advance();
                return new BooleanLiteral { Value = true, LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };

            case TokenType.False:
                Advance();
                return new BooleanLiteral { Value = false, LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };

            case TokenType.None:
                Advance();
                return new NoneLiteral { LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };

            case TokenType.Ellipsis:
                Advance();
                return new EllipsisLiteral { LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };

            case TokenType.Identifier:
                {
                    var name = Current.Value;
                    Advance();
                    return new Identifier { Name = name, LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                }

            case TokenType.LeftParen:
                {
                    Advance();

                    // Empty tuple ()
                    if (Current.Type == TokenType.RightParen)
                    {
                        Advance();
                        return new TupleLiteral { Elements = new List<Expression>(), LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                    }

                    var expr = ParseExpression();

                    // Tuple (expr,) or (expr, expr2, ...)
                    if (Current.Type == TokenType.Comma)
                    {
                        var elements = new List<Expression> { expr };

                        while (Current.Type == TokenType.Comma)
                        {
                            Advance();
                            if (Current.Type == TokenType.RightParen)
                                break;
                            elements.Add(ParseExpression());
                        }

                        Expect(TokenType.RightParen);
                        return new TupleLiteral { Elements = elements, LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                    }

                    Expect(TokenType.RightParen);
                    return new Parenthesized { Expression = expr, LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                }

            case TokenType.LeftBracket:
                {
                    Advance();

                    // Empty list []
                    if (Current.Type == TokenType.RightBracket)
                    {
                        Advance();
                        return new ListLiteral { Elements = new List<Expression>(), LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                    }

                    var firstExpr = ParseExpression();

                    // Check for list comprehension: [expr for x in iterable]
                    if (Current.Type == TokenType.For)
                    {
                        var clauses = ParseComprehensionClauses();
                        Expect(TokenType.RightBracket);
                        return new ListComprehension
                        {
                            Element = firstExpr,
                            Clauses = clauses,
                            LineStart = startLine,
                            ColumnStart = startColumn,
                            LineEnd = Current.Line,
                            ColumnEnd = Current.Column
                        };
                    }

                    // Regular list literal [elem1, elem2, ...]
                    var elements = new List<Expression> { firstExpr };

                    while (Current.Type == TokenType.Comma)
                    {
                        Advance();
                        if (Current.Type == TokenType.RightBracket)
                            break;
                        elements.Add(ParseExpression());
                    }

                    Expect(TokenType.RightBracket);
                    return new ListLiteral { Elements = elements, LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                }

            case TokenType.LeftBrace:
                {
                    Advance();

                    // Empty set {/} - special v0.5 syntax
                    if (Current.Type == TokenType.Slash)
                    {
                        Advance();
                        Expect(TokenType.RightBrace);
                        return new SetLiteral { Elements = new List<Expression>(), LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                    }

                    // Empty dict {}
                    if (Current.Type == TokenType.RightBrace)
                    {
                        Advance();
                        return new DictLiteral { Entries = new List<DictEntry>(), LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                    }

                    var firstExpr = ParseExpression();

                    // Dict {key: value, ...} or dict comprehension {key: value for x in iterable}
                    if (Current.Type == TokenType.Colon)
                    {
                        Advance();
                        var firstValue = ParseExpression();

                        // Check for dict comprehension: {key: value for x in iterable}
                        if (Current.Type == TokenType.For)
                        {
                            var clauses = ParseComprehensionClauses();
                            Expect(TokenType.RightBrace);
                            return new DictComprehension
                            {
                                Key = firstExpr,
                                Value = firstValue,
                                Clauses = clauses,
                                LineStart = startLine,
                                ColumnStart = startColumn,
                                LineEnd = Current.Line,
                                ColumnEnd = Current.Column
                            };
                        }

                        // Regular dict literal
                        var entries = new List<DictEntry> { new DictEntry { Key = firstExpr, Value = firstValue } };

                        while (Current.Type == TokenType.Comma)
                        {
                            Advance();
                            if (Current.Type == TokenType.RightBrace)
                                break;

                            var key = ParseExpression();
                            Expect(TokenType.Colon);
                            var value = ParseExpression();
                            entries.Add(new DictEntry { Key = key, Value = value });
                        }

                        Expect(TokenType.RightBrace);
                        return new DictLiteral { Entries = entries, LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                    }
                    // Set {elem1, elem2, ...} or set comprehension {expr for x in iterable}
                    else
                    {
                        // Check for set comprehension: {expr for x in iterable}
                        if (Current.Type == TokenType.For)
                        {
                            var clauses = ParseComprehensionClauses();
                            Expect(TokenType.RightBrace);
                            return new SetComprehension
                            {
                                Element = firstExpr,
                                Clauses = clauses,
                                LineStart = startLine,
                                ColumnStart = startColumn,
                                LineEnd = Current.Line,
                                ColumnEnd = Current.Column
                            };
                        }

                        // Regular set literal
                        var elements = new List<Expression> { firstExpr };

                        while (Current.Type == TokenType.Comma)
                        {
                            Advance();
                            if (Current.Type == TokenType.RightBrace)
                                break;
                            elements.Add(ParseExpression());
                        }

                        Expect(TokenType.RightBrace);
                        return new SetLiteral { Elements = elements, LineStart = startLine, ColumnStart = startColumn, LineEnd = Current.Line, ColumnEnd = Current.Column };
                    }
                }

            case TokenType.Lambda:
                {
                    Advance();
                    var parameters = new List<Parameter>();

                    // Parse lambda parameters
                    if (Current.Type != TokenType.Colon)
                    {
                        do
                        {
                            var name = ExpectIdentifier();
                            parameters.Add(new Parameter { Name = name });

                            if (Current.Type == TokenType.Comma)
                                Advance();
                            else
                                break;
                        } while (true);
                    }

                    Expect(TokenType.Colon);
                    var body = ParseExpression();

                    return new LambdaExpression
                    {
                        Parameters = parameters,
                        Body = body,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = body.LineEnd,
                        ColumnEnd = body.ColumnEnd
                    };
                }

            default:
                throw new ParserError($"Unexpected token: {Current.Type}", Current.Line, Current.Column);
        }
    }

    #endregion

    #region Type Annotation Parsing

    private TypeAnnotation ParseTypeAnnotation()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        // Handle 'auto' keyword for type inference
        string name;
        if (Current.Type == TokenType.Auto)
        {
            name = "auto";
            Advance();
        }
        // Handle 'None' as a type name (for -> None return annotations)
        else if (Current.Type == TokenType.None)
        {
            name = "None";
            Advance();
        }
        else
        {
            name = ExpectIdentifier();
        }

        var typeArgs = new List<TypeAnnotation>();
        var isNullable = false;

        // Generic type arguments [T, U]
        if (Current.Type == TokenType.LeftBracket)
        {
            Advance();
            do
            {
                typeArgs.Add(ParseTypeAnnotation());

                if (Current.Type == TokenType.Comma)
                    Advance();
                else
                    break;
            } while (true);
            Expect(TokenType.RightBracket);
        }

        // Nullable type suffix T?
        if (Current.Type == TokenType.Question)
        {
            isNullable = true;
            Advance();
        }

        var endLine = Peek(-1).Line;
        var endColumn = Peek(-1).Column + Peek(-1).Value.Length;

        return new TypeAnnotation
        {
            Name = name,
            TypeArguments = typeArgs,
            IsNullable = isNullable,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endLine,
            ColumnEnd = endColumn
        };
    }

    #endregion

    #region Helper Methods

    private void Advance() => _position++;

    private void Expect(TokenType type)
    {
        if (Current.Type != type)
            throw new ParserError($"Expected {type}, got {Current.Type}", Current.Line, Current.Column);
        Advance();
    }

    private string ExpectIdentifier()
    {
        if (Current.Type != TokenType.Identifier)
            throw new ParserError($"Expected identifier, got {Current.Type}", Current.Line, Current.Column);
        var value = Current.Value;
        Advance();
        return value;
    }

    private void ExpectNewline()
    {
        if (Current.Type == TokenType.Newline)
            Advance();
        else if (!IsAtEnd)
            throw new ParserError($"Expected newline, got {Current.Type}", Current.Line, Current.Column);
    }

    private void ExpectStatementEnd()
    {
        // Simple statements can end with:
        // 1. Newline (normal case)
        // 2. Dedent (last statement in a block)
        // 3. EOF (last statement in file)
        if (Current.Type == TokenType.Newline)
            Advance();
        else if (Current.Type != TokenType.Dedent && !IsAtEnd)
            throw new ParserError($"Expected end of statement, got {Current.Type}", Current.Line, Current.Column);
    }

    private void SkipNewlines()
    {
        while (Current.Type == TokenType.Newline)
            Advance();
    }

    private bool IsTypeName(string name)
    {
        // Primitive types
        if (name is "int" or "float" or "str" or "bool" or "list" or "dict" or "set" or "tuple" or "object" or "any")
            return true;

        // User-defined types typically start with uppercase letter
        if (name.Length > 0 && char.IsUpper(name[0]))
            return true;

        return false;
    }

    private List<FStringPart> ParseFStringParts(string fstringValue)
    {
        var parts = new List<FStringPart>();
        var i = 0;
        var textBuffer = new StringBuilder();

        while (i < fstringValue.Length)
        {
            if (fstringValue[i] == '{')
            {
                // Save any accumulated text before the expression
                if (textBuffer.Length > 0)
                {
                    parts.Add(new FStringPart { Text = textBuffer.ToString(), Expression = null });
                    textBuffer.Clear();
                }

                // Find the matching closing brace
                i++; // Skip '{'
                var exprStart = i;
                var braceDepth = 1;

                while (i < fstringValue.Length && braceDepth > 0)
                {
                    if (fstringValue[i] == '{') braceDepth++;
                    else if (fstringValue[i] == '}') braceDepth--;

                    if (braceDepth > 0) i++;
                }

                // Extract and parse the expression
                var exprText = fstringValue.Substring(exprStart, i - exprStart);

                // Parse the expression by creating a mini lexer/parser
                var exprLexer = new Lexer.Lexer(exprText);
                var exprTokens = new List<Token>();
                while (true)
                {
                    var token = exprLexer.NextToken();
                    exprTokens.Add(token);
                    if (token.Type == TokenType.Eof)
                        break;
                }

                var exprParser = new Parser(exprTokens);
                var expr = exprParser.ParseExpression();

                parts.Add(new FStringPart { Text = null, Expression = expr });

                i++; // Skip '}'
            }
            else
            {
                textBuffer.Append(fstringValue[i]);
                i++;
            }
        }

        // Add any remaining text
        if (textBuffer.Length > 0)
        {
            parts.Add(new FStringPart { Text = textBuffer.ToString(), Expression = null });
        }

        return parts;
    }

    #endregion
}
