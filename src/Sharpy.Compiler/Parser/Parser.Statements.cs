using System.Collections.Immutable;
using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Parser;

/// <summary>
/// Parser partial class: Statement parsing (control flow, imports)
/// </summary>
public partial class Parser
{
    private IfStatement ParseIfStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.If);
        var test = ParseExpression();
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var thenBody = ParseBlock();
        Expect(TokenType.Dedent);
        var endToken = Previous;

        var elifClauses = new List<ElifClause>();
        var elseBody = new List<Statement>();

        // Elif clauses
        while (Current.Type == TokenType.Elif)
        {
            var elifStartLine = Current.Line;
            var elifStartColumn = Current.Column;
            var elifStartToken = Current;
            Advance();
            var elifTest = ParseExpression();
            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            var elifBody = ParseBlock();
            Expect(TokenType.Dedent);
            var elifEndLine = Peek(-1).Line;
            var elifEndColumn = Peek(-1).Column + Peek(-1).Length;
            endToken = Previous;

            elifClauses.Add(new ElifClause
            {
                Test = elifTest,
                Body = elifBody.ToImmutableArray(),
                LineStart = elifStartLine,
                ColumnStart = elifStartColumn,
                LineEnd = elifEndLine,
                ColumnEnd = elifEndColumn,
                Span = GetSpanFromTokens(elifStartToken, Previous)
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
            endToken = Previous;
        }

        return new IfStatement
        {
            Test = test,
            ThenBody = thenBody.ToImmutableArray(),
            ElifClauses = elifClauses.ToImmutableArray(),
            ElseBody = elseBody.ToImmutableArray(),
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private WhileStatement ParseWhileStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.While);
        var test = ParseExpression();
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var body = ParseBlock();
        Expect(TokenType.Dedent);
        var endToken = Previous;

        // Optional else clause (runs if loop completes without break)
        var elseBody = new List<Statement>();
        if (Current.Type == TokenType.Else)
        {
            Advance();
            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            elseBody = ParseBlock();
            Expect(TokenType.Dedent);
            endToken = Previous;
        }

        return new WhileStatement
        {
            Test = test,
            Body = body.ToImmutableArray(),
            ElseBody = elseBody.ToImmutableArray(),
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private ForStatement ParseForStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

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
        var endToken = Previous;

        // Optional else clause (runs if loop completes without break)
        var elseBody = new List<Statement>();
        if (Current.Type == TokenType.Else)
        {
            Advance();
            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            elseBody = ParseBlock();
            Expect(TokenType.Dedent);
            endToken = Previous;
        }

        return new ForStatement
        {
            Target = target,
            Iterator = iterator,
            Body = body.ToImmutableArray(),
            ElseBody = elseBody.ToImmutableArray(),
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private Expression ParseForTarget()
    {
        // For target can be:
        // - Simple identifier: for x in ...
        // - Tuple: for x, y in ...
        // - Star unpacking: for first, *rest in ...
        // We parse up to but not including the 'in' keyword

        var startLine = Current.Line;
        var startColumn = Current.Column;

        var first = ParseForTargetElement();

        // Check if it's a tuple (comma-separated)
        if (Current.Type == TokenType.Comma)
        {
            var elements = new List<Expression> { first };

            while (Current.Type == TokenType.Comma)
            {
                Advance();
                if (Current.Type == TokenType.In)
                    break;  // Trailing comma before 'in'
                elements.Add(ParseForTargetElement());
            }

            return new TupleLiteral
            {
                Elements = elements.ToImmutableArray(),
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column,
                Span = CombineSpans(first.Span, elements[^1].Span)
            };
        }

        return first;
    }

    private Expression ParseForTargetElement()
    {
        if (Current.Type == TokenType.Star)
        {
            var starLine = Current.Line;
            var starColumn = Current.Column;
            var starToken = Current;
            Advance();
            var operand = ParseStoreTarget();
            return new StarExpression
            {
                Operand = operand,
                LineStart = starLine,
                ColumnStart = starColumn,
                LineEnd = operand.LineEnd,
                ColumnEnd = operand.ColumnEnd,
                Span = CombineSpans(GetSpanFromToken(starToken), operand.Span)
            };
        }

        return ParseStoreTarget();
    }

    /// <summary>
    /// Parses a store target: a primary expression optionally followed by <c>.attr</c> and
    /// <c>[index]</c> suffixes. Used by for-statement targets, comprehension targets, and
    /// <c>with … as</c> targets. Does not accept call <c>()</c> suffixes — you cannot
    /// assign to a function call result.
    /// </summary>
    private Expression ParseStoreTarget()
    {
        var expr = ParsePrimary();
        while (true)
        {
            if (!CheckLoopProgress())
                break;

            if (Current.Type == TokenType.Dot)
            {
                Advance();
                var memberToken = Current;
                if (Current.Type == TokenType.Identifier || IsKeywordToken(Current.Type))
                {
                    var member = Current.Value;
                    Advance();
                    expr = new MemberAccess
                    {
                        Object = expr,
                        Member = member,
                        IsNullConditional = false,
                        IsMemberBacktickEscaped = memberToken.IsBacktickEscaped,
                        MemberNameLineStart = memberToken.Line,
                        MemberNameColumnStart = memberToken.Column,
                        MemberNameColumnEnd = memberToken.Column + memberToken.Length,
                        LineStart = expr.LineStart,
                        ColumnStart = expr.ColumnStart,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Length,
                        Span = CombineSpans(expr.Span, GetSpanFromToken(Previous))
                    };
                }
                else
                {
                    ReportError($"Expected identifier after '.', got {Current.Type}", Current.Line, Current.Column,
                        DiagnosticCodes.Parser.ExpectedIdentifier, span: CurrentSpan);
                    break;
                }
            }
            else if (Current.Type == TokenType.LeftBracket)
            {
                var bracketToken = Current;
                Advance();
                var index = ParseExpression();
                Expect(TokenType.RightBracket);
                expr = new IndexAccess
                {
                    Object = expr,
                    Index = index,
                    LineStart = expr.LineStart,
                    ColumnStart = expr.ColumnStart,
                    LineEnd = Previous.Line,
                    ColumnEnd = Previous.Column + Previous.Length,
                    Span = CombineSpans(expr.Span, GetSpanFromToken(Previous))
                };
            }
            else
            {
                break;
            }
        }
        return expr;
    }

    /// <summary>
    /// Parse comprehension clauses: for x in iterable [if condition] [for y in iterable2] ...
    /// For now, only supporting single variable (no tuple unpacking in comprehensions)
    /// </summary>
    private List<ComprehensionClause> ParseComprehensionClauses()
    {
        var clauses = new List<ComprehensionClause>();

        var savedLoopPosition = _lastLoopPosition;
        _lastLoopPosition = -1;
        try
        {
            while (true)
            {
                if (!CheckLoopProgress())
                    break;

                // Detect an `async for` clause. Record the async marker (including the
                // `async` token position) so the resulting ForClause carries IsAsync = true
                // and spans the `async` keyword. Consumption of the trailing `for` happens
                // below in the shared ForClause branch.
                bool clauseIsAsync = false;
                int asyncStartLine = 0;
                int asyncStartColumn = 0;
                Token asyncStartToken = Current;
                if (Current.Type == TokenType.Async && Peek().Type == TokenType.For)
                {
                    clauseIsAsync = true;
                    asyncStartLine = Current.Line;
                    asyncStartColumn = Current.Column;
                    asyncStartToken = Current;
                    Advance();
                }

                if (Current.Type == TokenType.For)
                {
                    var startLine = clauseIsAsync ? asyncStartLine : Current.Line;
                    var startColumn = clauseIsAsync ? asyncStartColumn : Current.Column;
                    var startToken = clauseIsAsync ? asyncStartToken : Current;
                    Advance();

                    // Parse target (single identifier for now)
                    var target = ParseForTarget();

                    Expect(TokenType.In);
                    var iterator = ParseLogicalOr(); // Use lower precedence to avoid consuming too much

                    clauses.Add(new ForClause
                    {
                        Target = target,
                        Iterator = iterator,
                        IsAsync = clauseIsAsync,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Current.Line,
                        ColumnEnd = Current.Column,
                        Span = CombineSpans(GetSpanFromToken(startToken), iterator.Span)
                    });
                }
                else if (Current.Type == TokenType.If)
                {
                    var startLine = Current.Line;
                    var startColumn = Current.Column;
                    var startToken = Current;
                    Advance();

                    var condition = ParseLogicalOr(); // Use lower precedence to avoid consuming too much

                    clauses.Add(new IfClause
                    {
                        Condition = condition,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Current.Line,
                        ColumnEnd = Current.Column,
                        Span = CombineSpans(GetSpanFromToken(startToken), condition.Span)
                    });
                }
                else
                {
                    break;
                }
            }
        }
        finally
        {
            _lastLoopPosition = savedLoopPosition;
        }

        return clauses;
    }

    private WithStatement ParseWithStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.With);

        var items = new List<WithItem>();

        _lastLoopPosition = -1;
        do
        {
            if (!CheckLoopProgress())
                break;

            var itemStartLine = Current.Line;
            var itemStartColumn = Current.Column;
            var itemStartToken = Current;

            var contextExpr = ParseExpression();

            Expression? target = null;
            if (Current.Type == TokenType.As)
            {
                Advance();
                target = ParseStoreTarget();
            }

            var itemEndLine = Peek(-1).Line;
            var itemEndColumn = Peek(-1).Column + Peek(-1).Length;

            items.Add(new WithItem
            {
                ContextExpression = contextExpr,
                Target = target,
                LineStart = itemStartLine,
                ColumnStart = itemStartColumn,
                LineEnd = itemEndLine,
                ColumnEnd = itemEndColumn,
                Span = GetSpanFromTokens(itemStartToken, Previous)
            });

            if (Current.Type == TokenType.Comma)
                Advance();
            else
                break;
        } while (true);

        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var body = ParseBlock();
        Expect(TokenType.Dedent);
        var endToken = Previous;

        return new WithStatement
        {
            Items = items.ToImmutableArray(),
            Body = body.ToImmutableArray(),
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    /// <summary>
    /// Parses a defer statement in either the inline form (<c>defer f.close()</c>) or the
    /// block form (<c>defer:</c> followed by an indented suite). The statement is always
    /// parsed; whether it is permitted is decided by the feature gate (the experimental
    /// <c>defer</c> feature) during semantic analysis.
    /// </summary>
    private DeferStatement ParseDeferStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Defer);

        List<Statement> body;
        bool isBlock;
        if (Current.Type == TokenType.Colon)
        {
            // Block form: `defer:` <newline> <indent> suite <dedent>
            isBlock = true;
            Advance();
            ExpectNewline();
            Expect(TokenType.Indent);
            body = ParseBlock();
            Expect(TokenType.Dedent);
        }
        else
        {
            // Inline form: `defer <simple-statement>`. ParseSimpleStatement consumes the
            // trailing statement terminator.
            isBlock = false;
            body = new List<Statement> { ParseSimpleStatement() };
        }

        return new DeferStatement
        {
            Body = body.ToImmutableArray(),
            IsBlock = isBlock,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Length,
            Span = GetSpanFromTokens(startToken, Previous)
        };
    }

    private TryStatement ParseTryStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Try);
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var body = ParseBlock();
        Expect(TokenType.Dedent);
        var endToken = Previous;

        var handlers = new List<ExceptHandler>();

        while (Current.Type == TokenType.Except)
        {
            var handlerStartLine = Current.Line;
            var handlerStartColumn = Current.Column;
            var handlerStartToken = Current;
            Advance();

            // Check for except* (PEP 654)
            var isExceptStar = false;
            if (Current.Type == TokenType.Star)
            {
                isExceptStar = true;
                Advance(); // consume '*'
            }

            TypeAnnotation? exceptionType = null;
            string? name = null;
            bool nameEscaped = false;
            int nameLineStart = 0;
            int nameColumnStart = 0;
            int nameColumnEnd = 0;

            // except* requires a type — bare `except*:` is invalid
            if (isExceptStar && Current.Type == TokenType.Colon)
            {
                throw ReportError(
                    "'except*' requires an exception type",
                    handlerStartLine,
                    handlerStartColumn,
                    DiagnosticCodes.Parser.ExceptStarRequiresType,
                    span: CurrentSpan
                );
            }

            // except ExceptionType as name:
            // PEP 758: also allow comma-separated types without parentheses
            if (Current.Type != TokenType.Colon)
            {
                var firstTypeStart = Current;
                exceptionType = ParseTypeAnnotation();

                // Unparenthesized multiple exception types: except ValueError, TypeError:
                if (Current.Type == TokenType.Comma)
                {
                    var typeList = new List<TypeAnnotation> { exceptionType };
                    _lastLoopPosition = -1;
                    while (Current.Type == TokenType.Comma)
                    {
                        if (!CheckLoopProgress())
                            break;
                        Advance(); // consume ','
                        typeList.Add(ParseTypeAnnotation());
                    }

                    // 'as' requires parentheses when multiple types are listed without parens
                    if (Current.Type == TokenType.As)
                    {
                        throw ReportError(
                            "Use parentheses when combining multiple exception types with 'as': except (Type1, Type2) as e:",
                            Current.Line,
                            Current.Column,
                            DiagnosticCodes.Parser.ExceptWithAsRequiresParens,
                            span: CurrentSpan
                        );
                    }

                    var lastType = typeList[typeList.Count - 1];
                    exceptionType = new TypeAnnotation
                    {
                        Name = BuiltinNames.Tuple,
                        TypeArguments = typeList.ToImmutableArray(),
                        IsOptional = false,
                        LineStart = firstTypeStart.Line,
                        ColumnStart = firstTypeStart.Column,
                        LineEnd = lastType.LineEnd,
                        ColumnEnd = lastType.ColumnEnd,
                        Span = GetSpanFromTokens(firstTypeStart, Previous)
                    };
                }
                else if (Current.Type == TokenType.As)
                {
                    Advance();
                    var exceptNameToken = Current;
                    name = ExpectIdentifier();
                    nameEscaped = exceptNameToken.IsBacktickEscaped;
                    nameLineStart = exceptNameToken.Line;
                    nameColumnStart = exceptNameToken.Column;
                    nameColumnEnd = exceptNameToken.Column + exceptNameToken.Length;
                }
            }

            Expression? filter = null;
            if (Current.Type == TokenType.Identifier && Current.Value == "when")
            {
                if (isExceptStar)
                {
                    throw ReportError(
                        "'except*' handlers do not support 'when' filters",
                        Current.Line,
                        Current.Column,
                        DiagnosticCodes.Semantic.ExceptStarWhenNotSupported,
                        span: CurrentSpan
                    );
                }
                Advance();
                filter = ParseExpression();
            }

            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            var handlerBody = ParseBlock();
            Expect(TokenType.Dedent);
            var handlerEndLine = Peek(-1).Line;
            var handlerEndColumn = Peek(-1).Column + Peek(-1).Length;
            endToken = Previous;

            handlers.Add(new ExceptHandler
            {
                ExceptionType = exceptionType,
                Name = name,
                IsNameBacktickEscaped = nameEscaped,
                NameLineStart = nameLineStart,
                NameColumnStart = nameColumnStart,
                NameColumnEnd = nameColumnEnd,
                IsExceptStar = isExceptStar,
                Filter = filter,
                Body = handlerBody.ToImmutableArray(),
                LineStart = handlerStartLine,
                ColumnStart = handlerStartColumn,
                LineEnd = handlerEndLine,
                ColumnEnd = handlerEndColumn,
                Span = GetSpanFromTokens(handlerStartToken, Previous)
            });
        }

        // Validate: cannot mix except and except* in the same try block
        if (handlers.Count > 0)
        {
            var hasExceptStar = handlers.Any(h => h.IsExceptStar);
            var hasRegularExcept = handlers.Any(h => !h.IsExceptStar);
            if (hasExceptStar && hasRegularExcept)
            {
                var firstMixed = handlers.First(h => !h.IsExceptStar);
                throw ReportError(
                    "Cannot mix 'except' and 'except*' handlers in the same try block",
                    firstMixed.LineStart,
                    firstMixed.ColumnStart,
                    DiagnosticCodes.Parser.MixedExceptAndExceptStar,
                    span: firstMixed.Span
                );
            }
        }

        // else clause (runs if no exception raised in try block)
        var elseBody = new List<Statement>();
        if (Current.Type == TokenType.Else)
        {
            Advance();
            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            elseBody = ParseBlock();
            Expect(TokenType.Dedent);
            endToken = Previous;
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
            endToken = Previous;
        }

        return new TryStatement
        {
            Body = body.ToImmutableArray(),
            Handlers = handlers.ToImmutableArray(),
            ElseBody = elseBody.ToImmutableArray(),
            FinallyBody = finallyBody.ToImmutableArray(),
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private ReturnStatement ParseReturnStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var returnToken = Current;

        Expect(TokenType.Return);

        Expression? value = null;
        if (Current.Type != TokenType.Newline && Current.Type != TokenType.Dedent && !IsAtEnd)
            value = ParseExpressionOrBareTuple();

        ExpectStatementEnd();

        return new ReturnStatement
        {
            Value = value,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Length,
            Span = value != null
                ? CombineSpans(GetSpanFromToken(returnToken), value.Span)
                : GetSpanFromToken(returnToken)
        };
    }

    private YieldStatement ParseYieldStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var yieldToken = Current;

        Expect(TokenType.Yield);

        bool isFrom = false;
        if (Current.Type == TokenType.From)
        {
            isFrom = true;
            Advance();
        }

        var value = isFrom ? ParseExpression() : ParseExpressionOrBareTuple();

        ExpectStatementEnd();

        return new YieldStatement
        {
            Value = value,
            IsFrom = isFrom,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Length,
            Span = CombineSpans(GetSpanFromToken(yieldToken), value.Span)
        };
    }

    private RaiseStatement ParseRaiseStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var raiseToken = Current;

        Expect(TokenType.Raise);

        Expression? exception = null;
        Expression? cause = null;

        // A bare `raise` (re-raise) is terminated by a Dedent as well as by a Newline or EOF — the
        // same three-way guard ParseReturnStatement uses. Without the Dedent arm, `raise` as the
        // last line of a block in a file with no trailing newline tried to parse the Dedent as an
        // exception expression, so converting the terminator below would not have been enough.
        if (Current.Type != TokenType.Newline && Current.Type != TokenType.Dedent && !IsAtEnd)
        {
            exception = ParseExpression();

            // raise ... from cause — not supported in Sharpy
            if (Current.Type == TokenType.From)
            {
                var fromToken = Current;
                Advance();
                cause = ParseExpression(); // Parse for error recovery
                _diagnostics.AddError(
                    "'raise ... from ...' is not supported in Sharpy. Use 'raise' without a cause.",
                    GetSpanFromToken(fromToken),
                    fromToken.Line,
                    fromToken.Column,
                    code: DiagnosticCodes.Parser.RaiseFromNotSupported,
                    phase: CompilerPhase.Parser);
            }
        }

        ExpectStatementEnd();

        // Determine the span end based on what was parsed
        var endSpan = cause?.Span ?? exception?.Span ?? GetSpanFromToken(raiseToken);

        return new RaiseStatement
        {
            Exception = exception,
            Cause = cause,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Length,
            Span = CombineSpans(GetSpanFromToken(raiseToken), endSpan)
        };
    }

    private AssertStatement ParseAssertStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var assertToken = Current;

        Expect(TokenType.Assert);
        var test = ParseExpression();

        Expression? message = null;
        if (Current.Type == TokenType.Comma)
        {
            Advance();
            message = ParseExpression();
        }

        ExpectStatementEnd();

        var endSpan = message?.Span ?? test.Span;

        return new AssertStatement
        {
            Test = test,
            Message = message,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Length,
            Span = CombineSpans(GetSpanFromToken(assertToken), endSpan)
        };
    }

    private PassStatement ParsePassStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var passToken = Current;

        Expect(TokenType.Pass);
        ExpectStatementEnd();

        return new PassStatement
        {
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Length,
            Span = GetSpanFromToken(passToken)
        };
    }

    private BreakStatement ParseBreakStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var breakToken = Current;

        Expect(TokenType.Break);
        ExpectStatementEnd();

        return new BreakStatement
        {
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Length,
            Span = GetSpanFromToken(breakToken)
        };
    }

    private ContinueStatement ParseContinueStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var continueToken = Current;

        Expect(TokenType.Continue);
        ExpectStatementEnd();

        return new ContinueStatement
        {
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Length,
            Span = GetSpanFromToken(continueToken)
        };
    }

    private ImportStatement ParseImportStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Import);

        var names = new List<ImportAlias>();

        _lastLoopPosition = -1;
        do
        {
            if (!CheckLoopProgress())
                break;

            var aliasStartLine = Current.Line;
            var aliasStartColumn = Current.Column;
            var aliasStartToken = Current;
            var name = ParseDottedName(allowKeywords: true);
            string? asName = null;

            if (Current.Type == TokenType.As)
            {
                Advance();
                asName = ExpectIdentifier();
            }

            var aliasEndLine = Peek(-1).Line;
            var aliasEndColumn = Peek(-1).Column + Peek(-1).Length;

            names.Add(new ImportAlias
            {
                Name = name,
                AsName = asName,
                LineStart = aliasStartLine,
                ColumnStart = aliasStartColumn,
                LineEnd = aliasEndLine,
                ColumnEnd = aliasEndColumn,
                Span = GetSpanFromTokens(aliasStartToken, Previous)
            });

            if (Current.Type == TokenType.Comma)
                Advance();
            else
                break;
        } while (true);

        var endToken = Previous;
        ExpectStatementEnd();

        return new ImportStatement
        {
            Names = names.ToImmutableArray(),
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private FromImportStatement ParseFromImportStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.From);
        var moduleColStart = Current.Column;
        var module = ParseModuleName();
        var moduleColEnd = Previous.Column + Previous.Length;
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
            _lastLoopPosition = -1;
            do
            {
                if (!CheckLoopProgress())
                    break;

                var aliasStartLine = Current.Line;
                var aliasStartColumn = Current.Column;
                var aliasStartToken = Current;
                var name = ExpectIdentifier();
                string? asName = null;

                if (Current.Type == TokenType.As)
                {
                    Advance();
                    asName = ExpectIdentifier();
                }

                var aliasEndLine = Peek(-1).Line;
                var aliasEndColumn = Peek(-1).Column + Peek(-1).Length;

                names.Add(new ImportAlias
                {
                    Name = name,
                    AsName = asName,
                    LineStart = aliasStartLine,
                    ColumnStart = aliasStartColumn,
                    LineEnd = aliasEndLine,
                    ColumnEnd = aliasEndColumn,
                    Span = GetSpanFromTokens(aliasStartToken, Previous)
                });

                if (Current.Type == TokenType.Comma)
                    Advance();
                else
                    break;
            } while (true);
        }

        var endToken = Previous;
        ExpectStatementEnd();

        return new FromImportStatement
        {
            Module = module,
            ModuleColumnStart = moduleColStart,
            ModuleColumnEnd = moduleColEnd,
            Names = names.ToImmutableArray(),
            ImportAll = importAll,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private string ParseDottedName(bool allowKeywords = false)
    {
        var parts = new List<string> { allowKeywords && IsModuleNameKeyword(Current.Type) ? ExpectIdentifierOrKeyword() : ExpectIdentifier() };

        // A backtick-escaped identifier may already contain dots (e.g. `System.Collections.Generic`),
        // forming a complete dotted path. In that case it is the entire name (#713).
        if (Previous.IsBacktickEscaped && Previous.Value.Contains('.', StringComparison.Ordinal))
        {
            return parts[0];
        }

        while (Current.Type == TokenType.Dot)
        {
            Advance();
            parts.Add(ExpectIdentifier());
        }

        return string.Join(".", parts);
    }

    private string ParseModuleName()
    {
        // Handle relative imports with leading dots (e.g., ".helpers", "..utils")
        var leadingDots = new StringBuilder();
        while (Current.Type == TokenType.Dot)
        {
            leadingDots.Append('.');
            Advance();
        }

        // After the leading dots, there may be an identifier and more dotted parts
        // For example: ".helpers" or "..package.module"
        // But it's also valid to have just dots (e.g., "." for current package)
        if (Current.Type == TokenType.Identifier || IsModuleNameKeyword(Current.Type))
        {
            var dottedName = ParseDottedName(allowKeywords: true);
            return leadingDots.ToString() + dottedName;
        }

        // If we have leading dots, that's a valid relative import (e.g., "from . import something")
        if (leadingDots.Length > 0)
        {
            return leadingDots.ToString();
        }

        // No dots and no identifier means invalid syntax (e.g., "from import x")
        throw ReportError("Expected module name", Current.Line, Current.Column, DiagnosticCodes.Parser.ExpectedModuleName, span: CurrentSpan);
    }

    private List<Statement> ParseBlock()
    {
        var statements = new List<Statement>();

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
                statements.Add(ParseStatement());
            }
            catch (ParserAbortException)
            {
                // Error already recorded in _diagnostics by ReportError()

                // Stop after _maxErrors to avoid cascading false errors
                if (_diagnostics.ErrorCount >= _maxErrors)
                    break;

                // Panic-mode recovery: synchronize to next statement boundary
                Synchronize();
            }
            SkipNewlines();
        }

        return statements;
    }

    private List<Parameter> ParseParameters()
    {
        var parameters = new List<Parameter>();
        var hasVariadic = false;
        var seenSlash = false;
        var seenStar = false;

        if (Current.Type == TokenType.RightParen)
            return parameters;

        _lastLoopPosition = -1;
        do
        {
            if (!CheckLoopProgress())
                break;

            // Handle '/' positional-only marker
            if (Current.Type == TokenType.Slash)
            {
                if (parameters.Count == 0)
                    throw ReportError("'/' must have at least one parameter before it", Current.Line, Current.Column, DiagnosticCodes.Parser.SlashAtStart, span: CurrentSpan);
                if (seenSlash)
                    throw ReportError("'/' may only appear once in a parameter list", Current.Line, Current.Column, DiagnosticCodes.Parser.DuplicateSlashMarker, span: CurrentSpan);
                if (seenStar)
                    throw ReportError("'/' must appear before '*' in a parameter list", Current.Line, Current.Column, DiagnosticCodes.Parser.SlashAfterStar, span: CurrentSpan);

                seenSlash = true;
                Advance(); // Skip '/'

                // Mark all preceding parameters as positional-only
                for (var i = 0; i < parameters.Count; i++)
                    parameters[i] = parameters[i] with { Kind = ParameterKind.PositionalOnly };

                // Expect ',' or ')' after '/'
                if (Current.Type == TokenType.Comma)
                {
                    Advance();
                    if (Current.Type == TokenType.RightParen)
                        break;
                }
                else
                    break;

                continue;
            }

            // Handle bare '*' keyword-only marker (no identifier follows)
            if (Current.Type == TokenType.Star && Peek(1).Type != TokenType.Identifier)
            {
                if (seenStar)
                    throw ReportError("'*' may only appear once in a parameter list", Current.Line, Current.Column, DiagnosticCodes.Parser.DuplicateStarMarker, span: CurrentSpan);

                seenStar = true;
                Advance(); // Skip '*'

                // Expect ',' after bare '*' (at least one keyword-only param must follow)
                if (Current.Type == TokenType.Comma)
                {
                    Advance();
                    if (Current.Type == TokenType.RightParen)
                        break;
                }
                else
                    break;

                continue;
            }

            var startLine = Current.Line;
            var startColumn = Current.Column;
            var startToken = Current;

            // Check for variadic parameter (*args)
            var isVariadic = false;
            if (Current.Type == TokenType.Star)
            {
                if (hasVariadic)
                    throw ReportError("Only one variadic parameter (*args) is allowed per function", Current.Line, Current.Column, DiagnosticCodes.Parser.MultipleVariadic, span: CurrentSpan);
                isVariadic = true;
                hasVariadic = true;
                seenStar = true; // *args implicitly starts keyword-only section
                Advance();  // Skip *
            }

            var nameToken = Current;
            var name = ExpectIdentifier();
            TypeAnnotation? type = null;
            Expression? defaultValue = null;
            var modifier = ParameterModifier.None;

            if (Current.Type == TokenType.Colon)
            {
                Advance();

                // Check for parameter modifier after ':' (ref, out, in)
                // Syntax: name: ref type, name: out type, name: in type
                if (Current.Type == TokenType.Identifier
                    && (Current.Value == "ref" || Current.Value == "out")
                    && Peek().Type == TokenType.Identifier)
                {
                    modifier = Current.Value == "ref" ? ParameterModifier.Ref : ParameterModifier.Out;
                    Advance();
                }
                else if (Current.Type == TokenType.In
                         && Peek().Type == TokenType.Identifier)
                {
                    modifier = ParameterModifier.In;
                    Advance();
                }

                type = ParseTypeAnnotation();
            }

            bool isLateBound = false;

            if (Current.Type == TokenType.Assign)
            {
                if (isVariadic)
                    throw ReportError("Variadic parameter (*args) cannot have a default value", Current.Line, Current.Column, DiagnosticCodes.Parser.VariadicWithDefault, span: CurrentSpan);
                Advance();
                defaultValue = ParseExpression();
            }
            else if (Current.Type == TokenType.FatArrow)
            {
                // PEP 671: late-bound default — evaluated at call time, not definition time
                if (isVariadic)
                    throw ReportError("Variadic parameter (*args) cannot have a default value", Current.Line, Current.Column, DiagnosticCodes.Parser.VariadicWithDefault, span: CurrentSpan);
                Advance();
                defaultValue = ParseExpression();
                isLateBound = true;
            }

            var endToken = Previous;
            var endLine = endToken.Line;
            var endColumn = endToken.Column + endToken.Length;

            // Determine parameter kind based on slash/star markers
            var kind = ParameterKind.Normal;
            if (!isVariadic && seenStar)
                kind = ParameterKind.KeywordOnly;

            parameters.Add(new Parameter
            {
                Name = name,
                IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
                Type = type,
                DefaultValue = defaultValue,
                IsLateBound = isLateBound,
                IsVariadic = isVariadic,
                Kind = kind,
                Modifier = modifier,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = endLine,
                ColumnEnd = endColumn,
                // The name token, not `startToken`: for `*args` the start is the `*`, captured
                // at :1150-1152 BEFORE the star is consumed, so only the name token knows where
                // `args` actually begins and ends (#1359, #1454).
                NameLineStart = nameToken.Line,
                NameColumnStart = nameToken.Column,
                NameColumnEnd = nameToken.Column + nameToken.Length,
                Span = GetSpanFromTokens(startToken, endToken)
            });

            if (Current.Type == TokenType.Comma)
            {
                Advance();
                // Allow trailing comma: def foo(a, b, c,):
                if (Current.Type == TokenType.RightParen)
                    break;
            }
            else
                break;
        } while (true);

        return parameters;
    }

    private MatchStatement ParseMatchStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;
        Expect(TokenType.Match);

        var scrutinee = ParseExpression();
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);

        var cases = new List<MatchCase>();
        _lastLoopPosition = -1;
        while (Current.Type == TokenType.Case)
        {
            if (!CheckLoopProgress())
                break;
            cases.Add(ParseMatchCase());
        }

        if (cases.Count == 0)
            throw ReportError("Expected at least one 'case' clause in match statement",
                Current.Line, Current.Column,
                DiagnosticCodes.Parser.ExpectedCase, span: CurrentSpan);

        Expect(TokenType.Dedent);
        var endToken = Previous;

        return new MatchStatement
        {
            Scrutinee = scrutinee,
            Cases = cases.ToImmutableArray(),
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private MatchExpression ParseMatchExpression()
    {
        var startToken = Current;
        Expect(TokenType.Match);

        var scrutinee = ParseExpression();
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);

        var arms = new List<MatchArm>();
        _lastLoopPosition = -1;
        while (Current.Type == TokenType.Case)
        {
            if (!CheckLoopProgress())
                break;
            arms.Add(ParseMatchArm());
        }

        if (arms.Count == 0)
            throw ReportError("Expected at least one 'case' clause in match expression",
                Current.Line, Current.Column,
                DiagnosticCodes.Parser.ExpectedCase, span: CurrentSpan);

        Expect(TokenType.Dedent);
        var endToken = Previous;

        return new MatchExpression
        {
            Scrutinee = scrutinee,
            Arms = arms.ToImmutableArray(),
            LineStart = startToken.Line,
            ColumnStart = startToken.Column,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private MatchArm ParseMatchArm()
    {
        var startToken = Current;
        Expect(TokenType.Case);

        var pattern = ParsePattern();

        Expression? guard = null;
        if (Current.Type == TokenType.If)
        {
            Advance(); // skip 'if'
            guard = ParseExpression();
        }

        Expect(TokenType.Colon);
        var result = ParseExpression();
        // A block-consuming result (a nested match expression) already consumed the arm's newline
        // and its own Dedent, so the arm is terminated by that Dedent rather than a Newline (#1196).
        ExpectStatementEnd();
        var endToken = Previous;

        return new MatchArm
        {
            Pattern = pattern,
            Guard = guard,
            Result = result,
            LineStart = startToken.Line,
            ColumnStart = startToken.Column,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private MatchCase ParseMatchCase()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;
        Expect(TokenType.Case);

        var pattern = ParsePattern();

        Expression? guard = null;
        if (Current.Type == TokenType.If)
        {
            Advance(); // skip 'if'
            guard = ParseExpression();
        }

        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var body = ParseBlock();
        Expect(TokenType.Dedent);
        var endToken = Previous;

        return new MatchCase
        {
            Pattern = pattern,
            Guard = guard,
            Body = body.ToImmutableArray(),
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private Pattern ParsePattern()
    {
        // Precedence (PEP 634 `as_pattern: or_pattern 'as' NAME`): `as` is the OUTERMOST
        // combinator, '|' sits below it, and 'and' binds tighter than '|' (mirrors boolean
        // precedence). So `case A() | B() as w:` binds `w` to whichever alternative matched —
        // CPython's meaning — and `case int() as n | str() as n:` is a syntax error here as in
        // CPython (#1663): the first `as n` closes the pattern and the `|` has nowhere to go.
        // Parenthesize an alternative to bind inside it: `(int() as n) | (str() as n)`.
        var first = ParseAndPattern();

        if (Current.Type != TokenType.Pipe)
            return Current.Type == TokenType.As ? ParseAsSuffix(first) : first;

        var alternatives = new List<Pattern> { first };

        while (Current.Type == TokenType.Pipe)
        {
            Advance(); // consume '|'
            alternatives.Add(ParseAndPattern());
        }

        var lastAlt = alternatives[^1];
        Text.TextSpan? span = null;
        if (first.Span.HasValue && lastAlt.Span.HasValue)
        {
            span = new Text.TextSpan(first.Span.Value.Start, lastAlt.Span.Value.End - first.Span.Value.Start);
        }
        Pattern orPattern = new OrPattern
        {
            Alternatives = alternatives.ToImmutableArray(),
            LineStart = first.LineStart,
            ColumnStart = first.ColumnStart,
            LineEnd = lastAlt.LineEnd,
            ColumnEnd = lastAlt.ColumnEnd,
            Span = span
        };
        return Current.Type == TokenType.As ? ParseAsSuffix(orPattern) : orPattern;
    }

    /// <summary>
    /// Refuses a <c>|</c> that follows an <c>as</c> binding with a steer. The binding closed the
    /// pattern, so the alternative has nowhere to go — CPython reports a bare "invalid syntax";
    /// the message here names both valid spellings (#1663).
    /// </summary>
    private void RefuseAlternativeAfterAsBinding()
    {
        if (Current.Type != TokenType.Pipe)
            return;
        throw ReportError(
            "An 'as' binding closes the pattern, so '|' cannot follow it. Write "
            + "'A() | B() as name' to bind whichever alternative matched, or parenthesize the "
            + "alternative to bind inside it: '(A() as name) | (B() as name)'",
            Current.Line, Current.Column,
            DiagnosticCodes.Parser.ExpectedPattern, span: CurrentSpan);
    }

    /// <summary>
    /// Parses an and-pattern (<c>p1 and p2 and ...</c>), left-associative, binding tighter than
    /// '|' (#991). 'as' is not an operand here — it is the outermost combinator, parsed by
    /// <see cref="ParsePattern"/> after the or-pattern (#1663); parenthesize an operand to bind
    /// inside it. Returns the single pattern when no 'and' follows.
    /// </summary>
    private Pattern ParseAndPattern()
    {
        var left = ParseSinglePattern();

        while (Current.Type == TokenType.And)
        {
            Advance(); // consume 'and'
            var right = ParseSinglePattern();

            Text.TextSpan? span = null;
            if (left.Span.HasValue && right.Span.HasValue)
                span = new Text.TextSpan(left.Span.Value.Start, right.Span.Value.End - left.Span.Value.Start);

            left = new AndPattern
            {
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd,
                Span = span
            };
        }

        return left;
    }

    /// <summary>
    /// Parses the <c>as NAME</c> suffix that closes a pattern (the current token is <c>as</c>),
    /// wrapping <paramref name="inner"/> — a single, and-, or or-pattern — in an
    /// <see cref="AsPattern"/>. Called only from <see cref="ParsePattern"/>, so the binding scopes
    /// over everything the pattern matched (PEP 634).
    /// </summary>
    private Pattern ParseAsSuffix(Pattern inner)
    {
        Advance(); // consume 'as'
        if (Current.Type != TokenType.Identifier)
        {
            throw ReportError($"Expected identifier after 'as' in pattern, got '{Current.Value}'",
                Current.Line, Current.Column,
                DiagnosticCodes.Parser.ExpectedPattern, span: CurrentSpan);
        }
        var nameToken = Current;
        Advance();

        var name = new Ast.Identifier
        {
            Name = nameToken.Value,
            LineStart = nameToken.Line,
            ColumnStart = nameToken.Column,
            LineEnd = nameToken.Line,
            ColumnEnd = nameToken.Column + nameToken.Length,
            Span = GetSpanFromToken(nameToken)
        };
        RefuseAlternativeAfterAsBinding();

        Text.TextSpan? span = null;
        if (inner.Span.HasValue && name.Span.HasValue)
            span = new Text.TextSpan(inner.Span.Value.Start, name.Span.Value.End - inner.Span.Value.Start);

        return new AsPattern
        {
            Inner = inner,
            Name = name,
            LineStart = inner.LineStart,
            ColumnStart = inner.ColumnStart,
            LineEnd = nameToken.Line,
            ColumnEnd = nameToken.Column + nameToken.Length,
            Span = span
        };
    }

    private Pattern ParseSinglePattern()
    {
        switch (Current.Type)
        {
            case TokenType.LeftParen:
                return ParseTuplePattern();

            case TokenType.LeftBracket:
                return ParseListPattern();

            case TokenType.Integer:
            case TokenType.Float:
            case TokenType.String:
            case TokenType.True:
            case TokenType.False:
            case TokenType.None:
                return ParseLiteralPattern();

            case TokenType.Minus when Peek(1).Type == TokenType.Integer || Peek(1).Type == TokenType.Float:
                return ParseLiteralPattern();

            case TokenType.Identifier when Current.Value == "_":
                return ParseWildcardPattern();

            case TokenType.Identifier:
                return ParseIdentifierOrMemberAccessPattern();

            case TokenType.Greater:
            case TokenType.Less:
            case TokenType.GreaterEqual:
            case TokenType.LessEqual:
                return ParseRelationalPattern();

            default:
                throw ReportError($"Expected a pattern, got '{Current.Value}'",
                    Current.Line, Current.Column,
                    DiagnosticCodes.Parser.ExpectedPattern, span: CurrentSpan);
        }
    }


    private RelationalPattern ParseRelationalPattern()
    {
        var startToken = Current;
        var op = Current.Type switch
        {
            TokenType.Greater => RelationalOperator.GreaterThan,
            TokenType.GreaterEqual => RelationalOperator.GreaterThanOrEqual,
            TokenType.Less => RelationalOperator.LessThan,
            TokenType.LessEqual => RelationalOperator.LessThanOrEqual,
            _ => throw ReportError($"Expected relational operator, got '{Current.Value}'",
                Current.Line, Current.Column,
                DiagnosticCodes.Parser.ExpectedPattern, span: CurrentSpan)
        };
        Advance(); // consume operator

        var value = ParseUnary();
        var endToken = Previous;

        return new RelationalPattern
        {
            Operator = op,
            Value = value,
            LineStart = startToken.Line,
            ColumnStart = startToken.Column,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private WildcardPattern ParseWildcardPattern()
    {
        var token = Current;
        Advance();
        return new WildcardPattern
        {
            LineStart = token.Line,
            ColumnStart = token.Column,
            LineEnd = token.Line,
            ColumnEnd = token.Column + token.Length,
            Span = GetSpanFromToken(token)
        };
    }

    private Pattern ParseIdentifierOrMemberAccessPattern()
    {
        var token = Current;
        Advance();

        // Check for type pattern: identifier followed by '(' or '[' (generic type in pattern)
        if (Current.Type == TokenType.LeftParen || Current.Type == TokenType.LeftBracket)
        {
            return ParseTypePatternOrStructural(token);
        }

        if (Current.Type == TokenType.Dot)
        {
            // Parse dotted member access pattern (e.g., Color.RED)
            var parts = new List<string> { token.Value };
            Token endToken = token;

            while (Current.Type == TokenType.Dot)
            {
                Advance(); // consume '.'
                // Allow keyword tokens that can be union case names (e.g., Option.None)
                if (Current.Type != TokenType.Identifier && Current.Type != TokenType.None)
                {
                    throw ReportError($"Expected identifier after '.' in pattern, got '{Current.Value}'",
                        Current.Line, Current.Column,
                        DiagnosticCodes.Parser.ExpectedPattern, span: CurrentSpan);
                }
                endToken = Current;
                parts.Add(Current.Value);
                Advance();
            }

            // A dotted name followed by call parens is a CLASS pattern, not a value pattern:
            // `case lib.Circle():` names a type exactly as `case Circle():` does, and the module
            // qualifier is a lookup instruction rather than a change to what parses (#1445). The
            // discriminator is the same one the bare-identifier arm above uses, so
            // `case lib.Color.RED:` — no parens — keeps producing the MemberAccessPattern below.
            // A following `[` inherits the bare arm's GenericTypeInPattern diagnostic.
            if (Current.Type == TokenType.LeftParen || Current.Type == TokenType.LeftBracket)
            {
                return ParseTypePatternOrStructural(
                    token, string.Join(".", parts), endToken);
            }

            return new MemberAccessPattern
            {
                Parts = parts.ToImmutableArray(),
                LineStart = token.Line,
                ColumnStart = token.Column,
                LineEnd = endToken.Line,
                ColumnEnd = endToken.Column + endToken.Length,
                Span = GetSpanFromTokens(token, endToken)
            };
        }

        return new BindingPattern
        {
            Name = new Identifier
            {
                Name = token.Value,
                IsNameBacktickEscaped = token.IsBacktickEscaped,
                LineStart = token.Line,
                ColumnStart = token.Column,
                LineEnd = token.Line,
                ColumnEnd = token.Column + token.Length,
                Span = GetSpanFromToken(token)
            },
            LineStart = token.Line,
            ColumnStart = token.Column,
            LineEnd = token.Line,
            ColumnEnd = token.Column + token.Length,
            Span = GetSpanFromToken(token)
        };
    }


    /// <param name="qualifiedName">
    /// The dotted spelling when the pattern names its type through a module (<c>lib.Circle()</c>),
    /// so the <see cref="Ast.TypeAnnotation"/> carries the same name an annotation in that position
    /// would (#1445). Null for the ordinary bare-identifier arm.
    /// </param>
    /// <param name="nameEndToken">The last token of the name, for the annotation's span/escape flag.</param>
    private Pattern ParseTypePatternOrStructural(
        Token typeToken, string? qualifiedName = null, Token? nameEndToken = null)
    {
        var typeNameEnd = nameEndToken ?? typeToken;
        var typeAnnotation = new Ast.TypeAnnotation
        {
            Name = qualifiedName ?? typeToken.Value,
            IsNameBacktickEscaped = typeNameEnd.IsBacktickEscaped,
            LineStart = typeToken.Line,
            ColumnStart = typeToken.Column,
            LineEnd = typeNameEnd.Line,
            ColumnEnd = typeNameEnd.Column + typeNameEnd.Length,
            Span = GetSpanFromTokens(typeToken, typeNameEnd)
        };

        // Check for generic type arguments in patterns (e.g., Box[int]() in a case)
        if (Current.Type == TokenType.LeftBracket)
        {
            var bracketToken = Current;
            _diagnostics.AddError(
                "Generic type arguments are not supported in patterns. Use a wildcard or binding pattern instead.",
                GetSpanFromToken(bracketToken),
                bracketToken.Line,
                bracketToken.Column,
                code: DiagnosticCodes.Parser.GenericTypeInPattern,
                phase: CompilerPhase.Parser);

            // Skip tokens until matching ']' to recover
            var depth = 1;
            Advance(); // consume '['
            while (depth > 0 && Current.Type != TokenType.Eof)
            {
                Advance();
                if (Previous.Type == TokenType.LeftBracket)
                    depth++;
                else if (Previous.Type == TokenType.RightBracket)
                    depth--;
            }

            // If '(' follows, consume through matching ')' so we don't produce cascading errors
            if (Current.Type == TokenType.LeftParen)
            {
                var parenDepth = 1;
                Advance(); // consume '('
                while (parenDepth > 0 && Current.Type != TokenType.Eof)
                {
                    Advance();
                    if (Previous.Type == TokenType.LeftParen)
                        parenDepth++;
                    else if (Previous.Type == TokenType.RightParen)
                        parenDepth--;
                }
            }

            // Return a TypePattern for error recovery (without generic args);
            // 'as' binding is handled by ParseAsSuffix from ParsePattern.
            var endToken = Previous;
            return new TypePattern
            {
                Type = typeAnnotation,
                LineStart = typeToken.Line,
                ColumnStart = typeToken.Column,
                LineEnd = endToken.Line,
                ColumnEnd = endToken.Column + endToken.Length,
                Span = GetSpanFromTokens(typeToken, endToken)
            };
        }

        Expect(TokenType.LeftParen);

        // Check what's inside the parentheses
        if (Current.Type == TokenType.RightParen)
        {
            // Type() — pure type pattern; 'as' binding is handled by ParseAsSuffix from ParsePattern.
            Advance(); // consume ')'
            var endToken = Previous; // the ')'

            return new TypePattern
            {
                Type = typeAnnotation,
                LineStart = typeToken.Line,
                ColumnStart = typeToken.Column,
                LineEnd = endToken.Line,
                ColumnEnd = endToken.Column + endToken.Length,
                Span = GetSpanFromTokens(typeToken, endToken)
            };
        }

        // Check if this is a property pattern: identifier followed by '='
        if (Current.Type == TokenType.Identifier && Peek(1).Type == TokenType.Assign)
        {
            return ParsePropertyPattern(typeToken, typeAnnotation);
        }

        // Otherwise, positional pattern: comma-separated sub-patterns
        return ParsePositionalPattern(typeToken, typeAnnotation);
    }

    private PropertyPattern ParsePropertyPattern(Token typeToken, Ast.TypeAnnotation typeAnnotation)
    {
        var fields = new List<PropertyPatternField>();

        while (Current.Type != TokenType.RightParen && Current.Type != TokenType.Eof)
        {
            if (!CheckLoopProgress())
                break;

            if (Current.Type != TokenType.Identifier)
            {
                throw ReportError($"Expected field name in property pattern, got '{Current.Value}'",
                    Current.Line, Current.Column,
                    DiagnosticCodes.Parser.ExpectedIdentifier, span: CurrentSpan);
            }
            var fieldNameToken = Current;
            Advance(); // consume field name
            Expect(TokenType.Assign); // consume '='
            var fieldPattern = ParsePattern();

            fields.Add(new PropertyPatternField
            {
                Name = fieldNameToken.Value,
                Pattern = fieldPattern,
                LineStart = fieldNameToken.Line,
                ColumnStart = fieldNameToken.Column,
                LineEnd = Previous.Line,
                ColumnEnd = Previous.Column + Previous.Length,
                Span = GetSpanFromTokens(fieldNameToken, Previous)
            });

            if (Current.Type == TokenType.Comma)
            {
                Advance();
                if (Current.Type == TokenType.RightParen)
                    break; // trailing comma
            }
        }

        var endToken = Current; // the ')'
        Expect(TokenType.RightParen);

        return new PropertyPattern
        {
            Type = typeAnnotation,
            Fields = fields.ToImmutableArray(),
            LineStart = typeToken.Line,
            ColumnStart = typeToken.Column,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Length,
            Span = GetSpanFromTokens(typeToken, endToken)
        };
    }

    private PositionalPattern ParsePositionalPattern(Token typeToken, Ast.TypeAnnotation typeAnnotation)
    {
        var elements = new List<Pattern>();

        while (Current.Type != TokenType.RightParen && Current.Type != TokenType.Eof)
        {
            if (!CheckLoopProgress())
                break;

            elements.Add(ParsePattern());

            if (Current.Type == TokenType.Comma)
            {
                Advance();
                if (Current.Type == TokenType.RightParen)
                    break; // trailing comma
            }
        }

        var endToken = Current; // the ')'
        Expect(TokenType.RightParen);

        return new PositionalPattern
        {
            Type = typeAnnotation,
            Elements = elements.ToImmutableArray(),
            LineStart = typeToken.Line,
            ColumnStart = typeToken.Column,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Length,
            Span = GetSpanFromTokens(typeToken, endToken)
        };
    }

    private Pattern ParseTuplePattern()
    {
        var startToken = Current;
        Expect(TokenType.LeftParen);

        var elements = new List<Pattern>();
        var sawComma = false;
        if (Current.Type != TokenType.RightParen)
        {
            elements.Add(ParsePattern());
            while (Current.Type == TokenType.Comma)
            {
                sawComma = true;
                Advance();
                if (Current.Type == TokenType.RightParen)
                    break;
                elements.Add(ParsePattern());
            }
        }

        // RFC 3637: Single-element parenthesized pattern with guard → GuardPattern
        // Syntax: (pattern if guard)
        if (elements.Count == 1 && Current.Type == TokenType.If)
        {
            Advance(); // consume 'if'
            var guard = ParseExpression();
            var endToken = Current;
            Expect(TokenType.RightParen);
            return new GuardPattern
            {
                Inner = elements[0],
                Guard = guard,
                LineStart = startToken.Line,
                ColumnStart = startToken.Column,
                LineEnd = endToken.Line,
                ColumnEnd = endToken.Column + endToken.Length,
                Span = GetSpanFromTokens(startToken, endToken)
            };
        }

        var tupleEndToken = Current;
        Expect(TokenType.RightParen);

        // CPython group pattern (PEP 634): `(pattern)` with no comma IS the inner pattern — only a
        // trailing comma (`(x,)`) or two or more elements make a sequence pattern. Returning the
        // inner node keeps `case (y):` a capture (so the irrefutable-arm ordering rule sees it,
        // #1624) instead of a one-element tuple pattern refused on a non-tuple scrutinee.
        if (elements.Count == 1 && !sawComma)
            return elements[0];

        return new TuplePattern
        {
            Elements = elements.ToImmutableArray(),
            LineStart = startToken.Line,
            ColumnStart = startToken.Column,
            LineEnd = tupleEndToken.Line,
            ColumnEnd = tupleEndToken.Column + tupleEndToken.Length,
            Span = GetSpanFromTokens(startToken, tupleEndToken)
        };
    }

    /// <summary>
    /// Parses a list (sequence) pattern: <c>[]</c>, <c>[a]</c>, <c>[a, b]</c>, <c>[a, *rest]</c>,
    /// <c>[*init, last]</c>, <c>[a, *mid, b]</c> (#991). At most one <c>*</c> capture is allowed;
    /// the star's position is preserved by holding a <see cref="StarPattern"/> inline in Elements.
    /// </summary>
    private Pattern ParseListPattern()
    {
        var startToken = Current;
        Expect(TokenType.LeftBracket);

        var elements = new List<Pattern>();
        bool sawStar = false;
        if (Current.Type != TokenType.RightBracket)
        {
            elements.Add(ParseListElementPattern(ref sawStar));
            while (Current.Type == TokenType.Comma)
            {
                Advance();
                if (Current.Type == TokenType.RightBracket)
                    break;
                elements.Add(ParseListElementPattern(ref sawStar));
            }
        }

        var endToken = Current;
        Expect(TokenType.RightBracket);

        return new ListPattern
        {
            Elements = elements.ToImmutableArray(),
            LineStart = startToken.Line,
            ColumnStart = startToken.Column,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    /// <summary>
    /// Parses a single element of a list pattern: either a <c>*capture</c> star (at most one per
    /// list, enforced via <paramref name="sawStar"/>) or a nested pattern.
    /// </summary>
    private Pattern ParseListElementPattern(ref bool sawStar)
    {
        if (Current.Type == TokenType.Star)
        {
            if (sawStar)
            {
                throw ReportError("A list pattern can contain at most one '*' capture",
                    Current.Line, Current.Column,
                    DiagnosticCodes.Parser.MultipleStarsInPattern, span: CurrentSpan);
            }
            sawStar = true;

            var starToken = Current;
            Advance(); // consume '*'

            Pattern? capture = null;
            var endToken = starToken;
            if (Current.Type == TokenType.Identifier)
            {
                var nameToken = Current;
                Advance();
                endToken = nameToken;
                if (nameToken.Value == "_")
                {
                    capture = new WildcardPattern
                    {
                        LineStart = nameToken.Line,
                        ColumnStart = nameToken.Column,
                        LineEnd = nameToken.Line,
                        ColumnEnd = nameToken.Column + nameToken.Length,
                        Span = GetSpanFromToken(nameToken)
                    };
                }
                else
                {
                    capture = new BindingPattern
                    {
                        Name = new Identifier
                        {
                            Name = nameToken.Value,
                            IsNameBacktickEscaped = nameToken.IsBacktickEscaped,
                            LineStart = nameToken.Line,
                            ColumnStart = nameToken.Column,
                            LineEnd = nameToken.Line,
                            ColumnEnd = nameToken.Column + nameToken.Length,
                            Span = GetSpanFromToken(nameToken)
                        },
                        LineStart = nameToken.Line,
                        ColumnStart = nameToken.Column,
                        LineEnd = nameToken.Line,
                        ColumnEnd = nameToken.Column + nameToken.Length,
                        Span = GetSpanFromToken(nameToken)
                    };
                }
            }

            return new StarPattern
            {
                Capture = capture,
                LineStart = starToken.Line,
                ColumnStart = starToken.Column,
                LineEnd = endToken.Line,
                ColumnEnd = endToken.Column + endToken.Length,
                Span = GetSpanFromTokens(starToken, endToken)
            };
        }

        return ParsePattern();
    }

    private LiteralPattern ParseLiteralPattern()
    {
        var startToken = Current;
        var literal = ParseUnary();
        var endToken = Previous;

        return new LiteralPattern
        {
            Literal = literal,
            LineStart = startToken.Line,
            ColumnStart = startToken.Column,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }
}
