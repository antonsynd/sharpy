using System.Collections.Immutable;
using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Parser;

/// <summary>
/// Parser partial class: Definition parsing (functions, classes, structs, interfaces, enums)
/// </summary>
public partial class Parser
{
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
            if (Current.Type >= TokenType.Assign && Current.Type <= TokenType.NullCoalesceAssign)
            {
                // This is a tuple unpacking assignment
                var tuple = new TupleLiteral
                {
                    Elements = elements.ToImmutableArray(),
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = Current.Line,
                    ColumnEnd = Current.Column,
                    Span = CombineSpans(elements[0].Span, elements[^1].Span)
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
                    ColumnEnd = value.ColumnEnd,
                    Span = CombineSpans(tuple.Span, value.Span)
                };
            }

            // If not an assignment, this is an error (tuple expression statements not allowed)
            throw ReportError("Tuple expression not allowed as a statement", Current.Line, Current.Column, DiagnosticCodes.Parser.TupleAsStatement);
        }

        // Check for assignment operators
        if (Current.Type >= TokenType.Assign && Current.Type <= TokenType.NullCoalesceAssign)
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
                ColumnEnd = value.ColumnEnd,
                Span = CombineSpans(expr.Span, value.Span)
            };
        }

        // Check for type annotation (variable declaration)
        if (Current.Type == TokenType.Colon)
        {
            if (expr is not Identifier id)
                throw ReportError("Invalid type annotation target", Current.Line, Current.Column, DiagnosticCodes.Parser.InvalidTypeAnnotationTarget);

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
                LineEnd = Previous.Line,
                ColumnEnd = Previous.Column + Previous.Value.Length,
                Span = initialValue != null
                    ? CombineSpans(id.Span, initialValue.Span)
                    : id.Span  // TypeAnnotation doesn't have Span yet (A.12)
            };
        }

        ExpectStatementEnd();

        return new ExpressionStatement
        {
            Expression = expr,
            LineStart = expr.LineStart,
            ColumnStart = expr.ColumnStart,
            LineEnd = expr.LineEnd,
            ColumnEnd = expr.ColumnEnd,
            Span = expr.Span
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
        TokenType.NullCoalesceAssign => AssignmentOperator.NullCoalesceAssign,
        _ => throw ReportError($"Not an assignment operator: {type}", Current.Line, Current.Column, DiagnosticCodes.Parser.UnexpectedToken)
    };

    private FunctionDef ParseFunctionDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Def);
        var name = ExpectIdentifier();

        var typeParams = new List<TypeParameterDef>();

        // Type parameters [T, U] or [T: IComparable, U: class]
        if (Current.Type == TokenType.LeftBracket)
        {
            typeParams = ParseTypeParameterList();
        }

        Expect(TokenType.LeftParen);

        var parameters = ParseParameters();
        Expect(TokenType.RightParen);

        TypeAnnotation? returnType = null;
        if (Current.Type == TokenType.Arrow)
        {
            Advance();
            returnType = ParseTypeAnnotation();
        }

        // For interface methods, the colon and body are optional.
        // If we're parsing an interface and we see a newline instead of a colon,
        // synthesize an ellipsis body to represent the abstract method signature.
        if (_parsingInterface && Current.Type != TokenType.Colon)
        {
            // Interface method without explicit body - synthesize ellipsis body
            ExpectNewline();

            var ellipsisExpr = new EllipsisLiteral
            {
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = startLine,
                ColumnEnd = startColumn
            };

            return new FunctionDef
            {
                Name = name,
                TypeParameters = typeParams.ToImmutableArray(),
                Parameters = parameters.ToImmutableArray(),
                ReturnType = returnType,
                Body = ImmutableArray.Create<Statement>(
                    new ExpressionStatement
                    {
                        Expression = ellipsisExpr,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = startLine,
                        ColumnEnd = startColumn
                    }
                ),
                DocString = null,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = Previous.Line,
                ColumnEnd = Previous.Column,
                Span = GetSpanFromTokens(startToken, Previous)
            };
        }

        Expect(TokenType.Colon);

        // Support inline ellipsis syntax: def foo(): ...
        if (Current.Type == TokenType.Ellipsis)
        {
            var ellipsisLine = Current.Line;
            var ellipsisColumn = Current.Column;
            var ellipsisToken = Current;
            Advance(); // consume '...'
            ExpectNewline();

            return new FunctionDef
            {
                Name = name,
                TypeParameters = typeParams.ToImmutableArray(),
                Parameters = parameters.ToImmutableArray(),
                ReturnType = returnType,
                Body = ImmutableArray.Create<Statement>(
                    new ExpressionStatement
                    {
                        Expression = new EllipsisLiteral
                        {
                            LineStart = ellipsisLine,
                            ColumnStart = ellipsisColumn,
                            LineEnd = ellipsisLine,
                            ColumnEnd = ellipsisColumn + 3,
                            Span = GetSpanFromToken(ellipsisToken)
                        },
                        LineStart = ellipsisLine,
                        ColumnStart = ellipsisColumn,
                        LineEnd = ellipsisLine,
                        ColumnEnd = ellipsisColumn + 3,
                        Span = GetSpanFromToken(ellipsisToken)
                    }
                ),
                DocString = null,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column,
                Span = GetSpanFromTokens(startToken, ellipsisToken)
            };
        }

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
        var endToken = Previous;

        return new FunctionDef
        {
            Name = name,
            TypeParameters = typeParams.ToImmutableArray(),
            Parameters = parameters.ToImmutableArray(),
            ReturnType = returnType,
            Body = body.ToImmutableArray(),
            DocString = docString,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private ClassDef ParseClassDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Class);
        var name = ExpectIdentifier();

        var typeParams = new List<TypeParameterDef>();
        var baseClasses = new List<TypeAnnotation>();

        // Type parameters [T, U] or [T: IComparable, U: class]
        if (Current.Type == TokenType.LeftBracket)
        {
            typeParams = ParseTypeParameterList();
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
        var endToken = Previous;

        return new ClassDef
        {
            Name = name,
            TypeParameters = typeParams.ToImmutableArray(),
            BaseClasses = baseClasses.ToImmutableArray(),
            Body = body.ToImmutableArray(),
            DocString = docString,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private StructDef ParseStructDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Struct);
        var name = ExpectIdentifier();

        var typeParams = new List<TypeParameterDef>();
        var baseInterfaces = new List<TypeAnnotation>();

        // Type parameters [T, U] or [T: IComparable, U: class]
        if (Current.Type == TokenType.LeftBracket)
        {
            typeParams = ParseTypeParameterList();
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
        var endToken = Previous;

        return new StructDef
        {
            Name = name,
            TypeParameters = typeParams.ToImmutableArray(),
            BaseClasses = baseInterfaces.ToImmutableArray(),
            Body = body.ToImmutableArray(),
            DocString = docString,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private InterfaceDef ParseInterfaceDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Interface);
        var name = ExpectIdentifier();

        var typeParams = new List<TypeParameterDef>();
        var baseInterfaces = new List<TypeAnnotation>();

        // Type parameters [T, U] or [T: IComparable, U: class]
        if (Current.Type == TokenType.LeftBracket)
        {
            typeParams = ParseTypeParameterList();
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

        // Set interface parsing flag so ParseFunctionDef knows to allow bodyless methods
        _parsingInterface = true;
        List<Statement> body;
        try
        {
            body = ParseBlock();
        }
        finally
        {
            _parsingInterface = false;
        }
        Expect(TokenType.Dedent);
        var endToken = Previous;

        return new InterfaceDef
        {
            Name = name,
            TypeParameters = typeParams.ToImmutableArray(),
            BaseInterfaces = baseInterfaces.ToImmutableArray(),
            Body = body.ToImmutableArray(),
            DocString = docString,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private List<TypeParameterDef> ParseTypeParameterList()
    {
        var typeParams = new List<TypeParameterDef>();

        Expect(TokenType.LeftBracket);

        do
        {
            var paramStartLine = Current.Line;
            var paramStartColumn = Current.Column;
            var paramStartToken = Current;

            var paramName = ExpectIdentifier();
            var constraints = new List<ConstraintClause>();

            // Check for constraint: T: IComparable
            if (Current.Type == TokenType.Colon)
            {
                Advance(); // consume ':'
                constraints = ParseConstraints();
            }

            var paramEndToken = Previous;

            typeParams.Add(new TypeParameterDef
            {
                Name = paramName,
                Constraints = constraints.ToImmutableArray(),
                LineStart = paramStartLine,
                ColumnStart = paramStartColumn,
                LineEnd = paramEndToken.Line,
                ColumnEnd = paramEndToken.Column + paramEndToken.Value.Length,
                Span = GetSpanFromTokens(paramStartToken, paramEndToken)
            });

            if (Current.Type == TokenType.Comma)
                Advance();
            else
                break;
        } while (true);

        Expect(TokenType.RightBracket);

        return typeParams;
    }

    private List<ConstraintClause> ParseConstraints()
    {
        var constraints = new List<ConstraintClause>();

        do
        {
            constraints.Add(ParseSingleConstraint());

            if (Current.Type == TokenType.Ampersand)
                Advance(); // consume '&'
            else
                break;
        } while (true);

        return constraints;
    }

    private ConstraintClause ParseSingleConstraint()
    {
        // class constraint
        if (Current.Type == TokenType.Class)
        {
            Advance();
            return new ClassConstraint();
        }

        // struct constraint
        if (Current.Type == TokenType.Struct)
        {
            Advance();
            return new StructConstraint();
        }

        // new() constraint
        if (Current.Type == TokenType.Identifier && Current.Value == "new")
        {
            Advance();
            Expect(TokenType.LeftParen);
            Expect(TokenType.RightParen);
            return new NewConstraint();
        }

        // Type constraint (interface or base type)
        var type = ParseTypeAnnotation();
        return new TypeConstraint { Type = type };
    }

    private EnumDef ParseEnumDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

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
            SkipNewlines();
            if (Current.Type == TokenType.Dedent || IsAtEnd)
                break;

            try
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
                var memberStartToken = Current;
                var memberName = ExpectIdentifier();
                Expression? value = null;

                if (Current.Type == TokenType.Assign)
                {
                    Advance();
                    value = ParseExpression();
                }

                var memberEndToken = Previous;
                var memberEndLine = memberEndToken.Line;
                var memberEndColumn = memberEndToken.Column + memberEndToken.Value.Length;

                members.Add(new EnumMember
                {
                    Name = memberName,
                    Value = value,
                    LineStart = memberStartLine,
                    ColumnStart = memberStartColumn,
                    LineEnd = memberEndLine,
                    ColumnEnd = memberEndColumn,
                    Span = GetSpanFromTokens(memberStartToken, memberEndToken)
                });
                ExpectNewline();
            }
            catch (ParserAbortException)
            {
                // Error already recorded. Skip to the next line within the enum body.
                if (_diagnostics.ErrorCount >= _maxErrors)
                    break;
                Synchronize();
            }
            SkipNewlines();
        }

        Expect(TokenType.Dedent);
        var endToken = Previous;

        // Validate enum has at least one member
        if (members.Count == 0)
        {
            throw ReportError($"Enum '{name}' must have at least one member", startLine, startColumn, DiagnosticCodes.Parser.EmptyEnum);
        }

        return new EnumDef
        {
            Name = name,
            Members = members.ToImmutableArray(),
            DocString = docString,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private TypeAlias ParseTypeAlias()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Type);
        var name = ExpectIdentifier();
        Expect(TokenType.Assign);

        // Check if this is a function type: (params) -> returnType
        TypeAnnotation? type = null;
        FunctionType? functionType = null;

        if (Current.Type == TokenType.LeftParen)
        {
            // Parse function type: (int, str) -> bool
            Advance(); // consume '('
            var paramTypes = new List<TypeAnnotation>();

            // Parse parameter types
            if (Current.Type != TokenType.RightParen)
            {
                do
                {
                    paramTypes.Add(ParseTypeAnnotation());

                    if (Current.Type == TokenType.Comma)
                        Advance();
                    else
                        break;
                } while (true);
            }

            Expect(TokenType.RightParen);
            Expect(TokenType.Arrow);
            var returnType = ParseTypeAnnotation();

            functionType = new FunctionType
            {
                ParameterTypes = paramTypes.ToImmutableArray(),
                ReturnType = returnType
            };
        }
        else
        {
            // Parse regular type annotation
            type = ParseTypeAnnotation();
        }

        var endToken = Previous;
        ExpectNewline();

        return new TypeAlias
        {
            Name = name,
            Type = type,
            FunctionType = functionType,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private VariableDeclaration ParseConstDeclaration()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Const);
        var name = ExpectIdentifier();

        // Type annotation is optional for const declarations
        // const X: int = 1  (explicit type)
        // const X = "MyApp" (type inferred)
        TypeAnnotation? type = null;
        if (Current.Type == TokenType.Colon)
        {
            Advance(); // consume ':'
            type = ParseTypeAnnotation();
        }

        Expect(TokenType.Assign);
        var value = ParseExpression();
        var endToken = Previous;
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
            ColumnEnd = Current.Column,
            Span = CombineSpans(GetSpanFromToken(startToken), value.Span)
        };
    }

}
