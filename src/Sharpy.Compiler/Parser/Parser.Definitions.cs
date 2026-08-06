using System.Collections.Immutable;
using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

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

        // Check for star expression at the start: *rest, x = items
        Expression expr;
        if (Current.Type == TokenType.Star)
        {
            var starLine = Current.Line;
            var starColumn = Current.Column;
            var starToken = Current;
            Advance();
            var operand = ParsePrimary();
            expr = new StarExpression
            {
                Operand = operand,
                LineStart = starLine,
                ColumnStart = starColumn,
                LineEnd = operand.LineEnd,
                ColumnEnd = operand.ColumnEnd,
                Span = CombineSpans(GetSpanFromToken(starToken), operand.Span)
            };
        }
        else
        {
            expr = ParseExpression();
        }

        // Check for tuple unpacking: x, y = ...
        // If we see a comma after the expression, it might be a tuple target for assignment
        if (Current.Type == TokenType.Comma)
        {
            var startLine = expr.LineStart;
            var startColumn = expr.ColumnStart;
            var elements = new List<Expression> { expr };

            // Parse remaining tuple elements (with star support)
            while (Current.Type == TokenType.Comma)
            {
                Advance();
                if (Current.Type == TokenType.Star)
                {
                    var starLine = Current.Line;
                    var starColumn = Current.Column;
                    var starToken = Current;
                    Advance();
                    var operand = ParsePrimary();
                    elements.Add(new StarExpression
                    {
                        Operand = operand,
                        LineStart = starLine,
                        ColumnStart = starColumn,
                        LineEnd = operand.LineEnd,
                        ColumnEnd = operand.ColumnEnd,
                        Span = CombineSpans(GetSpanFromToken(starToken), operand.Span)
                    });
                }
                else
                {
                    elements.Add(ParseExpression());
                }
            }

            // Now check if we have an assignment operator
            if (Current.Type >= TokenType.Assign && Current.Type <= TokenType.AtAssign)
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
                var value = ParseExpressionOrBareTuple();
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
            throw ReportError("Tuple expression not allowed as a statement", Current.Line, Current.Column, DiagnosticCodes.Parser.TupleAsStatement, span: CurrentSpan);
        }

        // Check for assignment operators
        if (Current.Type >= TokenType.Assign && Current.Type <= TokenType.AtAssign)
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
                throw ReportError("Invalid type annotation target", Current.Line, Current.Column, DiagnosticCodes.Parser.InvalidTypeAnnotationTarget, span: CurrentSpan);

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
                NameLineStart = id.LineStart,
                NameColumnStart = id.ColumnStart,
                IsNameBacktickEscaped = id.IsNameBacktickEscaped,
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
        TokenType.AtAssign => AssignmentOperator.MatMulAssign,
        _ => throw ReportError($"Not an assignment operator: {type}", Current.Line, Current.Column, DiagnosticCodes.Parser.UnexpectedToken, span: CurrentSpan)
    };

    /// <summary>
    /// A parsed inline stub body: the single-statement body and the token the body ended on
    /// (the <c>...</c> itself, or the closing paren of a grouped spelling), which callers need
    /// for the enclosing declaration's span.
    /// </summary>
    private readonly record struct InlineStubBody(ImmutableArray<Statement> Body, Token EndToken);

    /// <summary>
    /// Parses the inline stub body of a declaration written on one line — <c>def f(): ...</c>,
    /// <c>property get p(self) -> int: ...</c>, <c>event add e(self, h: H): ...</c>. Call it
    /// immediately after the declaration's <c>:</c>. Returns <c>null</c> without consuming anything
    /// when what follows is not a stub, so the caller falls through to its ordinary
    /// <c>ExpectNewline()</c> + indented-block path exactly as before.
    ///
    /// <para>Those three forms used to carry three independently written
    /// <c>Current.Type == TokenType.Ellipsis</c> guards, which is why <c>(...)</c> — the same stub
    /// as <c>...</c> at every other seam since #1214 — was a parse error here and only here
    /// (#1238). Classification is delegated to <see cref="AstHelper.TryGetEllipsisStub"/> rather
    /// than re-implemented as a lookahead past balanced parens: this position asks the same
    /// authority every block position asks, so it cannot drift from it again.</para>
    ///
    /// <para>The token gate below is not a stub test — it only asks whether an inline body can
    /// begin here at all. Anything else (<c>pass</c>, <c>return 1</c>, <c>1 + 1</c>) is left
    /// wholly unconsumed so the caller reports the same SPY0102 "Expected newline, got X" it always
    /// did, and a grouped expression that turns out not to be a stub (<c>(1 + 2)</c>) is rewound to
    /// the same effect. An unterminated group (<c>(...</c>) is the one input whose diagnostic
    /// changes: it now reports the missing <c>)</c> instead of "Expected newline, got LeftParen".
    /// It was an error before and remains one — see <c>InlineStubParserTests</c>, which pins every
    /// cell of this behavior.</para>
    /// </summary>
    private InlineStubBody? TryParseInlineStubBody()
    {
        if (Current.Type is not (TokenType.Ellipsis or TokenType.LeftParen))
            return null;

        var savedPosition = _position;
        var expression = ParseExpression();
        var statement = new ExpressionStatement
        {
            Expression = expression,
            LineStart = expression.LineStart,
            ColumnStart = expression.ColumnStart,
            LineEnd = expression.LineEnd,
            ColumnEnd = expression.ColumnEnd,
            Span = expression.Span
        };

        if (!AstHelper.TryGetEllipsisStub(statement, out _))
        {
            _position = savedPosition;
            return null;
        }

        var endToken = Previous;
        ExpectNewline();
        return new InlineStubBody(ImmutableArray.Create<Statement>(statement), endToken);
    }

    private FunctionDef ParseAsyncFunctionDef()
    {
        var asyncToken = Current;
        Expect(TokenType.Async);
        var funcDef = ParseFunctionDef();
        return funcDef with
        {
            IsAsync = true,
            LineStart = asyncToken.Line,
            ColumnStart = asyncToken.Column,
            Span = CombineSpans(GetSpanFromToken(asyncToken), funcDef.Span)
                ?? GetSpanFromToken(asyncToken)
        };
    }

    private FunctionDef ParseFunctionDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Def);
        var nameToken = Current;
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
            return CreateBodylessFunctionDef(
                name,
                nameToken.Line,
                nameToken.Column,
                nameToken.IsBacktickEscaped,
                typeParams.ToImmutableArray(),
                parameters.ToImmutableArray(),
                returnType,
                startLine,
                startColumn,
                startToken);
        }

        // For @abstract methods in classes, the colon and body are optional.
        // e.g., @abstract\ndef foo(self) -> int
        if (Current.Type != TokenType.Colon && HasAbstractDecorator())
        {
            return CreateBodylessFunctionDef(
                name,
                nameToken.Line,
                nameToken.Column,
                nameToken.IsBacktickEscaped,
                typeParams.ToImmutableArray(),
                parameters.ToImmutableArray(),
                returnType,
                startLine,
                startColumn,
                startToken);
        }

        Expect(TokenType.Colon);

        // Support inline stub syntax: def foo(): ...  (and every grouped spelling of it — #1238)
        if (TryParseInlineStubBody() is { } inlineStub)
        {
            return new FunctionDef
            {
                Name = name,
                NameLineStart = nameToken.Line,
                NameColumnStart = nameToken.Column,
                IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
                TypeParameters = typeParams.ToImmutableArray(),
                Parameters = parameters.ToImmutableArray(),
                ReturnType = returnType,
                Body = inlineStub.Body,
                DocString = null,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column,
                Span = GetSpanFromTokens(startToken, inlineStub.EndToken)
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
            NameLineStart = nameToken.Line,
            NameColumnStart = nameToken.Column,
            IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
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

    /// <summary>
    /// Creates a FunctionDef with a synthesized ellipsis body (for body-less methods in interfaces or @abstract methods).
    /// </summary>
    private FunctionDef CreateBodylessFunctionDef(
        string name,
        int nameLineStart,
        int nameColumnStart,
        bool isNameBacktickEscaped,
        ImmutableArray<TypeParameterDef> typeParams,
        ImmutableArray<Parameter> parameters,
        TypeAnnotation? returnType,
        int startLine,
        int startColumn,
        Token startToken)
    {
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
            NameLineStart = nameLineStart,
            NameColumnStart = nameColumnStart,
            IsNameBacktickEscaped = isNameBacktickEscaped,
            TypeParameters = typeParams,
            Parameters = parameters,
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

    private ClassDef ParseClassDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Class);
        var nameToken = Current;
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
                _lastLoopPosition = -1;
                do
                {
                    if (!CheckLoopProgress())
                        break;

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
            NameLineStart = nameToken.Line,
            NameColumnStart = nameToken.Column,
            IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
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
        var nameToken = Current;
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
                _lastLoopPosition = -1;
                do
                {
                    if (!CheckLoopProgress())
                        break;

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
            NameLineStart = nameToken.Line,
            NameColumnStart = nameToken.Column,
            IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
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
        var nameToken = Current;
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
                _lastLoopPosition = -1;
                do
                {
                    if (!CheckLoopProgress())
                        break;

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
            NameLineStart = nameToken.Line,
            NameColumnStart = nameToken.Column,
            IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
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

        _lastLoopPosition = -1;
        do
        {
            if (!CheckLoopProgress())
                break;

            var paramStartLine = Current.Line;
            var paramStartColumn = Current.Column;
            var paramStartToken = Current;

            // Check for variance annotation: out T (covariant) or in T (contravariant)
            var variance = TypeParameterVariance.None;
            if (Current.Type == TokenType.Identifier && Current.Value == "out")
            {
                variance = TypeParameterVariance.Covariant;
                Advance();
            }
            else if (Current.Type == TokenType.In)
            {
                variance = TypeParameterVariance.Contravariant;
                Advance();
            }

            var paramName = ExpectIdentifier();
            var constraints = new List<ConstraintClause>();

            // Check for constraint: T: IComparable
            if (Current.Type == TokenType.Colon)
            {
                Advance(); // consume ':'
                constraints = ParseConstraints();
            }

            // Check for default type: T = int (PEP 696)
            TypeAnnotation? defaultType = null;
            if (Current.Type == TokenType.Assign)
            {
                Advance(); // consume '='
                defaultType = ParseTypeAnnotation();
            }

            var paramEndToken = Previous;

            typeParams.Add(new TypeParameterDef
            {
                Name = paramName,
                Constraints = constraints.ToImmutableArray(),
                Variance = variance,
                DefaultType = defaultType,
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

        _lastLoopPosition = -1;
        do
        {
            if (!CheckLoopProgress())
                break;

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

        // notnull constraint
        if (Current.Type == TokenType.Identifier && Current.Value == "notnull")
        {
            Advance();
            return new NotnullConstraint();
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
        var nameToken = Current;
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

        _lastLoopPosition = -1;
        while (Current.Type != TokenType.Dedent && !IsAtEnd)
        {
            if (!CheckLoopProgress())
                break;

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
            throw ReportError($"Enum '{name}' must have at least one member", startLine, startColumn, DiagnosticCodes.Parser.EmptyEnum, span: CurrentSpan);
        }

        return new EnumDef
        {
            Name = name,
            NameLineStart = nameToken.Line,
            NameColumnStart = nameToken.Column,
            IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
            Members = members.ToImmutableArray(),
            DocString = docString,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private UnionDef ParseUnionDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Union);
        var nameToken = Current;
        var name = ExpectIdentifier();

        // Optional type parameters: union Result[T, E]:
        var typeParams = ImmutableArray<TypeParameterDef>.Empty;
        if (Current.Type == TokenType.LeftBracket)
        {
            typeParams = ParseTypeParameterList().ToImmutableArray();
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

        var cases = new List<UnionCaseDef>();
        var body = new List<Statement>();

        _lastLoopPosition = -1;
        while (Current.Type != TokenType.Dedent && !IsAtEnd)
        {
            if (!CheckLoopProgress())
                break;

            SkipNewlines();
            if (Current.Type == TokenType.Dedent || IsAtEnd)
                break;

            try
            {
                // Handle pass statement in empty union body
                if (Current.Type == TokenType.Pass)
                {
                    Advance();
                    ExpectNewline();
                    SkipNewlines();
                    continue;
                }

                // Method definitions inside the union body.
                if (Current.Type == TokenType.Def)
                {
                    body.Add(ParseFunctionDef());
                    SkipNewlines();
                    continue;
                }

                if (Current.Type == TokenType.Async)
                {
                    body.Add(ParseAsyncFunctionDef());
                    SkipNewlines();
                    continue;
                }

                var caseStartLine = Current.Line;
                var caseStartColumn = Current.Column;
                var caseStartToken = Current;

                Expect(TokenType.Case);
                var caseNameToken = Current;
                var caseName = ExpectIdentifier();

                var fields = new List<UnionCaseField>();

                // Optional field list: case Circle(radius: float)
                if (Current.Type == TokenType.LeftParen)
                {
                    Advance(); // consume '('

                    if (Current.Type != TokenType.RightParen)
                    {
                        _lastLoopPosition = -1;
                        do
                        {
                            if (!CheckLoopProgress())
                                break;

                            var fieldStartLine = Current.Line;
                            var fieldStartColumn = Current.Column;
                            var fieldStartToken = Current;

                            var fieldName = ExpectIdentifier();
                            Expect(TokenType.Colon);
                            var fieldType = ParseTypeAnnotation();

                            var fieldEndToken = Previous;

                            fields.Add(new UnionCaseField
                            {
                                Name = fieldName,
                                Type = fieldType,
                                LineStart = fieldStartLine,
                                ColumnStart = fieldStartColumn,
                                LineEnd = fieldEndToken.Line,
                                ColumnEnd = fieldEndToken.Column + fieldEndToken.Value.Length,
                                Span = GetSpanFromTokens(fieldStartToken, fieldEndToken)
                            });

                            if (Current.Type == TokenType.Comma)
                                Advance();
                            else
                                break;
                        } while (true);
                    }

                    Expect(TokenType.RightParen);
                }

                var caseEndToken = Previous;

                cases.Add(new UnionCaseDef
                {
                    Name = caseName,
                    NameLineStart = caseNameToken.Line,
                    NameColumnStart = caseNameToken.Column,
                    Fields = fields.ToImmutableArray(),
                    LineStart = caseStartLine,
                    ColumnStart = caseStartColumn,
                    LineEnd = caseEndToken.Line,
                    ColumnEnd = caseEndToken.Column + caseEndToken.Value.Length,
                    Span = GetSpanFromTokens(caseStartToken, caseEndToken)
                });

                ExpectNewline();
            }
            catch (ParserAbortException)
            {
                // Error already recorded. Skip to the next line within the union body.
                if (_diagnostics.ErrorCount >= _maxErrors)
                    break;
                Synchronize();
            }
            SkipNewlines();
        }

        Expect(TokenType.Dedent);
        var endToken = Previous;

        // Validate union has at least one case
        if (cases.Count == 0)
        {
            throw ReportError($"Union '{name}' must have at least one case", startLine, startColumn, DiagnosticCodes.Parser.EmptyUnion, span: CurrentSpan);
        }

        return new UnionDef
        {
            Name = name,
            NameLineStart = nameToken.Line,
            NameColumnStart = nameToken.Column,
            IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
            TypeParameters = typeParams,
            Cases = cases.ToImmutableArray(),
            Body = body.ToImmutableArray(),
            DocString = docString,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private DelegateDef ParseDelegateDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Delegate);
        var nameToken = Current;
        var name = ExpectIdentifier();

        // Optional type parameters: delegate Predicate[T](item: T) -> bool
        var typeParams = new List<TypeParameterDef>();
        if (Current.Type == TokenType.LeftBracket)
        {
            typeParams = ParseTypeParameterList();
        }

        Expect(TokenType.LeftParen);
        var parameters = ParseParameters();
        Expect(TokenType.RightParen);

        // Optional return type: -> type
        TypeAnnotation? returnType = null;
        if (Current.Type == TokenType.Arrow)
        {
            Advance();
            returnType = ParseTypeAnnotation();
        }

        var endToken = Previous;

        return new DelegateDef
        {
            Name = name,
            NameLineStart = nameToken.Line,
            NameColumnStart = nameToken.Column,
            IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
            TypeParameters = typeParams.ToImmutableArray(),
            Parameters = parameters.ToImmutableArray(),
            ReturnType = returnType,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Value.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private TypeAlias ParseTypeAlias()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Type);
        var nameToken = Current;
        var name = ExpectIdentifier();

        // Optional type parameters: type Cb[T] = (T) -> None
        var typeParams = new List<TypeParameterDef>();
        if (Current.Type == TokenType.LeftBracket)
        {
            typeParams = ParseTypeParameterList();
        }

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
                _lastLoopPosition = -1;
                do
                {
                    if (!CheckLoopProgress())
                        break;

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
            NameLineStart = nameToken.Line,
            NameColumnStart = nameToken.Column,
            TypeParameters = typeParams.ToImmutableArray(),
            Type = type,
            FunctionType = functionType,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private PropertyDef ParsePropertyDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Property);

        // Check for accessor keyword: get, set, or init
        var accessor = PropertyAccessor.None;
        if (Current.Type == TokenType.Identifier)
        {
            switch (Current.Value)
            {
                case "get":
                    accessor = PropertyAccessor.Get;
                    Advance();
                    break;
                case "set":
                    accessor = PropertyAccessor.Set;
                    Advance();
                    break;
                case "init":
                    accessor = PropertyAccessor.Init;
                    Advance();
                    break;
            }
        }

        // Read property name (identifier)
        var nameToken = Current;
        var name = ExpectIdentifier();

        // Check for explicit interface: Name.Name pattern
        string? explicitInterface = null;
        if (Current.Type == TokenType.Dot)
        {
            Advance(); // consume '.'
            explicitInterface = name;
            nameToken = Current;
            name = ExpectIdentifier();
        }

        // Function-style property: property get name(self) -> type: body
        if (Current.Type == TokenType.LeftParen)
        {
            Advance(); // consume '('
            var parameters = ParseParameters();
            Expect(TokenType.RightParen);

            TypeAnnotation? returnType = null;
            if (Current.Type == TokenType.Arrow)
            {
                Advance();
                returnType = ParseTypeAnnotation();
            }

            // For interface properties, the colon and body are optional.
            if (_parsingInterface && Current.Type != TokenType.Colon)
            {
                // Interface property without explicit body - synthesize ellipsis body
                ExpectNewline();

                var ellipsisExpr = new EllipsisLiteral
                {
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = startLine,
                    ColumnEnd = startColumn
                };

                return new PropertyDef
                {
                    Name = name,
                    NameLineStart = nameToken.Line,
                    NameColumnStart = nameToken.Column,
                    IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
                    Accessor = accessor,
                    IsFunctionStyle = true,
                    Parameters = parameters.ToImmutableArray(),
                    ReturnType = returnType,
                    ExplicitInterface = explicitInterface,
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
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = Previous.Line,
                    ColumnEnd = Previous.Column,
                    Span = GetSpanFromTokens(startToken, Previous)
                };
            }

            Expect(TokenType.Colon);

            // Support inline stub syntax: property get name(self) -> type: ...  (#1238)
            if (TryParseInlineStubBody() is { } inlineStub)
            {
                return new PropertyDef
                {
                    Name = name,
                    NameLineStart = nameToken.Line,
                    NameColumnStart = nameToken.Column,
                    IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
                    Accessor = accessor,
                    IsFunctionStyle = true,
                    Parameters = parameters.ToImmutableArray(),
                    ReturnType = returnType,
                    ExplicitInterface = explicitInterface,
                    Body = inlineStub.Body,
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = Current.Line,
                    ColumnEnd = Current.Column,
                    Span = GetSpanFromTokens(startToken, inlineStub.EndToken)
                };
            }

            ExpectNewline();
            Expect(TokenType.Indent);
            var body = ParseBlock();
            Expect(TokenType.Dedent);
            var endToken = Previous;

            return new PropertyDef
            {
                Name = name,
                NameLineStart = nameToken.Line,
                NameColumnStart = nameToken.Column,
                IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
                Accessor = accessor,
                IsFunctionStyle = true,
                Parameters = parameters.ToImmutableArray(),
                ReturnType = returnType,
                ExplicitInterface = explicitInterface,
                Body = body.ToImmutableArray(),
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column,
                Span = GetSpanFromTokens(startToken, endToken)
            };
        }

        // Auto-property: property name: type = default
        Expect(TokenType.Colon);
        var type = ParseTypeAnnotation();

        Expression? defaultValue = null;
        if (Current.Type == TokenType.Assign)
        {
            Advance();
            defaultValue = ParseExpression();
        }

        var autoEndToken = Previous;

        // Optional observer block: the auto-property header is followed by a deeper-indented
        // suite of `before_set`/`after_set` clauses. Detected by a NEWLINE immediately followed
        // by an INDENT (nothing else may be indented under an auto-property). Always parsed; the
        // feature gate rejects use unless `property_observers` is enabled.
        var observers = ImmutableArray<PropertyObserver>.Empty;
        if (Current.Type == TokenType.Newline && Peek().Type == TokenType.Indent)
        {
            Advance(); // consume NEWLINE
            Expect(TokenType.Indent);
            var observerList = new List<PropertyObserver>();
            _lastLoopPosition = -1;
            while (Current.Type != TokenType.Dedent && !IsAtEnd)
            {
                if (!CheckLoopProgress())
                    break;
                SkipNewlines();
                if (Current.Type == TokenType.Dedent || IsAtEnd)
                    break;
                observerList.Add(ParsePropertyObserver());
                SkipNewlines();
            }
            Expect(TokenType.Dedent);
            autoEndToken = Previous;
            observers = observerList.ToImmutableArray();
        }
        else
        {
            ExpectStatementEnd();
        }

        return new PropertyDef
        {
            Name = name,
            NameLineStart = nameToken.Line,
            NameColumnStart = nameToken.Column,
            IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
            Accessor = accessor,
            Type = type,
            DefaultValue = defaultValue,
            IsFunctionStyle = false,
            ExplicitInterface = explicitInterface,
            Observers = observers,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = autoEndToken.Line,
            ColumnEnd = autoEndToken.Column + autoEndToken.Value.Length,
            Span = CombineSpans(GetSpanFromToken(startToken), GetSpanFromToken(autoEndToken))
        };
    }

    /// <summary>
    /// Parses one property observer clause (<c>before_set(&lt;param&gt;):</c> or
    /// <c>after_set(&lt;param&gt;):</c> followed by an indented suite). <c>before_set</c> and
    /// <c>after_set</c> are contextual identifiers — special only here, in observer position —
    /// so no dedicated token type exists. The parameter is required and user-named (no magic
    /// <c>oldvalue</c> keyword). The clause is always parsed; whether it is permitted is decided
    /// by the feature gate (the experimental <c>property_observers</c> feature) later.
    /// </summary>
    private PropertyObserver ParsePropertyObserver()
    {
        var startToken = Current;

        if (Current.Type != TokenType.Identifier ||
            (Current.Value != "before_set" && Current.Value != "after_set"))
        {
            throw ReportError(
                $"Expected 'before_set' or 'after_set' property observer, got '{Current.Value}'",
                Current.Line, Current.Column, DiagnosticCodes.Parser.UnexpectedToken, span: CurrentSpan);
        }

        var kind = Current.Value == "before_set" ? ObserverKind.BeforeSet : ObserverKind.AfterSet;
        Advance();

        Expect(TokenType.LeftParen);
        var paramToken = Current;
        var paramName = ExpectIdentifier();
        Expect(TokenType.RightParen);
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var body = ParseBlock();
        Expect(TokenType.Dedent);
        var endToken = Previous;

        return new PropertyObserver(kind, paramName, body.ToImmutableArray())
        {
            LineStart = startToken.Line,
            ColumnStart = startToken.Column,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Value.Length,
            ParamNameLine = paramToken.Line,
            ParamNameColumn = paramToken.Column,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private EventDef ParseEventDef()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Event);

        // Check for accessor keyword: add or remove
        var accessor = EventAccessor.None;
        if (Current.Type == TokenType.Identifier)
        {
            switch (Current.Value)
            {
                case "add":
                    accessor = EventAccessor.Add;
                    Advance();
                    break;
                case "remove":
                    accessor = EventAccessor.Remove;
                    Advance();
                    break;
            }
        }

        // Read event name
        var nameToken = Current;
        var name = ExpectIdentifier();

        // Function-style event: event add name(self, handler: T): body
        if (Current.Type == TokenType.LeftParen)
        {
            if (accessor == EventAccessor.None)
            {
                throw ReportError(
                    "Function-style event requires 'add' or 'remove' accessor keyword",
                    Current.Line, Current.Column,
                    DiagnosticCodes.Parser.FunctionStyleEventWithoutAccessor,
                    GetSpanFromToken(Current));
            }

            Advance(); // consume '('
            var parameters = ParseParameters();
            Expect(TokenType.RightParen);

            // For interface events, the colon and body are optional.
            if (_parsingInterface && Current.Type != TokenType.Colon)
            {
                ExpectNewline();

                var ellipsisExpr = new EllipsisLiteral
                {
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = startLine,
                    ColumnEnd = startColumn
                };

                return new EventDef
                {
                    Name = name,
                    NameLineStart = nameToken.Line,
                    NameColumnStart = nameToken.Column,
                    IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
                    Accessor = accessor,
                    IsFunctionStyle = true,
                    Parameters = parameters.ToImmutableArray(),
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = Previous.Line,
                    ColumnEnd = Previous.Column,
                    Span = GetSpanFromTokens(startToken, Previous)
                };
            }

            Expect(TokenType.Colon);

            // Support inline stub syntax: event add name(self, handler: T): ...  (#1238)
            if (TryParseInlineStubBody() is { } inlineStub)
            {
                return new EventDef
                {
                    Name = name,
                    NameLineStart = nameToken.Line,
                    NameColumnStart = nameToken.Column,
                    IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
                    Accessor = accessor,
                    IsFunctionStyle = true,
                    Parameters = parameters.ToImmutableArray(),
                    Body = inlineStub.Body,
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = Current.Line,
                    ColumnEnd = Current.Column,
                    Span = GetSpanFromTokens(startToken, inlineStub.EndToken)
                };
            }

            ExpectNewline();
            Expect(TokenType.Indent);
            var body = ParseBlock();
            Expect(TokenType.Dedent);
            var endToken = Previous;

            return new EventDef
            {
                Name = name,
                NameLineStart = nameToken.Line,
                NameColumnStart = nameToken.Column,
                IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
                Accessor = accessor,
                IsFunctionStyle = true,
                Parameters = parameters.ToImmutableArray(),
                Body = body.ToImmutableArray(),
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column,
                Span = GetSpanFromTokens(startToken, endToken)
            };
        }

        // Auto-event: event name: DelegateType
        if (accessor != EventAccessor.None)
        {
            throw ReportError(
                "Auto-event must not have an accessor keyword; use 'event name: DelegateType'",
                startToken.Line, startToken.Column,
                DiagnosticCodes.Parser.AutoEventWithBody,
                GetSpanFromToken(startToken));
        }

        Expect(TokenType.Colon);
        var type = ParseTypeAnnotation();

        var autoEndToken = Previous;
        ExpectStatementEnd();

        return new EventDef
        {
            Name = name,
            NameLineStart = nameToken.Line,
            NameColumnStart = nameToken.Column,
            IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
            Accessor = EventAccessor.None,
            Type = type,
            IsFunctionStyle = false,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = autoEndToken.Line,
            ColumnEnd = autoEndToken.Column + autoEndToken.Value.Length,
            Span = CombineSpans(GetSpanFromToken(startToken), GetSpanFromToken(autoEndToken))
        };
    }

    private VariableDeclaration ParseConstDeclaration()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Const);
        var nameToken = Current;
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
            NameLineStart = nameToken.Line,
            NameColumnStart = nameToken.Column,
            IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
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
