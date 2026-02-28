using System.Collections.Immutable;
using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

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
            var elifEndColumn = Peek(-1).Column + Peek(-1).Value.Length;
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
            ColumnEnd = Previous.Column + Previous.Value.Length,
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
            ColumnEnd = Previous.Column + Previous.Value.Length,
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
            ColumnEnd = Previous.Column + Previous.Value.Length,
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
            var operand = ParsePrimary();
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

        return ParsePrimary();
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

                if (Current.Type == TokenType.For)
                {
                    var startLine = Current.Line;
                    var startColumn = Current.Column;
                    var startToken = Current;
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

            // Inhibit postfix 'as' (type cast) so it's not consumed as part of the expression.
            // In with statements, 'as' binds the context manager to a name.
            var savedInhibit = _inhibitPostfixAs;
            _inhibitPostfixAs = true;
            var contextExpr = ParseExpression();
            _inhibitPostfixAs = savedInhibit;

            string? name = null;
            if (Current.Type == TokenType.As)
            {
                Advance();
                name = ExpectIdentifier();
            }

            var itemEndLine = Peek(-1).Line;
            var itemEndColumn = Peek(-1).Column + Peek(-1).Value.Length;

            items.Add(new WithItem
            {
                ContextExpression = contextExpr,
                Name = name,
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
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = GetSpanFromTokens(startToken, endToken)
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
            endToken = Previous;

            handlers.Add(new ExceptHandler
            {
                ExceptionType = exceptionType,
                Name = name,
                Body = handlerBody.ToImmutableArray(),
                LineStart = handlerStartLine,
                ColumnStart = handlerStartColumn,
                LineEnd = handlerEndLine,
                ColumnEnd = handlerEndColumn,
                Span = GetSpanFromTokens(handlerStartToken, Previous)
            });
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
            ColumnEnd = Previous.Column + Previous.Value.Length,
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
            value = ParseExpression();

        ExpectStatementEnd();

        return new ReturnStatement
        {
            Value = value,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
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

        var value = ParseExpression();

        ExpectStatementEnd();

        return new YieldStatement
        {
            Value = value,
            IsFrom = isFrom,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
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

        if (Current.Type != TokenType.Newline && !IsAtEnd)
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

        ExpectNewline();

        // Determine the span end based on what was parsed
        var endSpan = cause?.Span ?? exception?.Span ?? GetSpanFromToken(raiseToken);

        return new RaiseStatement
        {
            Exception = exception,
            Cause = cause,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
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

        ExpectNewline();

        var endSpan = message?.Span ?? test.Span;

        return new AssertStatement
        {
            Test = test,
            Message = message,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
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
            ColumnEnd = Previous.Column + Previous.Value.Length,
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
            ColumnEnd = Previous.Column + Previous.Value.Length,
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
            ColumnEnd = Previous.Column + Previous.Value.Length,
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
                ColumnEnd = aliasEndColumn,
                Span = GetSpanFromTokens(aliasStartToken, Previous)
            });

            if (Current.Type == TokenType.Comma)
                Advance();
            else
                break;
        } while (true);

        var endToken = Previous;
        ExpectNewline();

        return new ImportStatement
        {
            Names = names.ToImmutableArray(),
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Value.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private FromImportStatement ParseFromImportStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.From);
        var module = ParseModuleName();
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
                var aliasEndColumn = Peek(-1).Column + Peek(-1).Value.Length;

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
        ExpectNewline();

        return new FromImportStatement
        {
            Module = module,
            Names = names.ToImmutableArray(),
            ImportAll = importAll,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Value.Length,
            Span = GetSpanFromTokens(startToken, endToken)
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
        if (Current.Type == TokenType.Identifier)
        {
            var dottedName = ParseDottedName();
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

        if (Current.Type == TokenType.RightParen)
            return parameters;

        _lastLoopPosition = -1;
        do
        {
            if (!CheckLoopProgress())
                break;

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
                Advance();  // Skip *
            }

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
                if (isVariadic)
                    throw ReportError("Variadic parameter (*args) cannot have a default value", Current.Line, Current.Column, DiagnosticCodes.Parser.VariadicWithDefault, span: CurrentSpan);
                Advance();
                defaultValue = ParseExpression();
            }

            var endToken = Previous;
            var endLine = endToken.Line;
            var endColumn = endToken.Column + endToken.Value.Length;

            parameters.Add(new Parameter
            {
                Name = name,
                Type = type,
                DefaultValue = defaultValue,
                IsVariadic = isVariadic,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = endLine,
                ColumnEnd = endColumn,
                Span = GetSpanFromTokens(startToken, endToken)
            });

            if (Current.Type == TokenType.Comma)
            {
                if (isVariadic)
                    throw ReportError("Variadic parameter (*args) must be the last parameter", Current.Line, Current.Column, DiagnosticCodes.Parser.VariadicNotLast, span: CurrentSpan);
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
            ColumnEnd = endToken.Column + endToken.Value.Length,
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
            ColumnEnd = endToken.Column + endToken.Value.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private Pattern ParsePattern()
    {
        var first = ParseSinglePattern();

        if (Current.Type != TokenType.Pipe)
            return first;

        var alternatives = new List<Pattern> { first };

        while (Current.Type == TokenType.Pipe)
        {
            Advance(); // consume '|'
            alternatives.Add(ParseSinglePattern());
        }

        var lastAlt = alternatives[^1];
        Text.TextSpan? span = null;
        if (first.Span.HasValue && lastAlt.Span.HasValue)
        {
            span = new Text.TextSpan(first.Span.Value.Start, lastAlt.Span.Value.End - first.Span.Value.Start);
        }
        return new OrPattern
        {
            Alternatives = alternatives.ToImmutableArray(),
            LineStart = first.LineStart,
            ColumnStart = first.ColumnStart,
            LineEnd = lastAlt.LineEnd,
            ColumnEnd = lastAlt.ColumnEnd,
            Span = span
        };
    }

    private Pattern ParseSinglePattern()
    {
        switch (Current.Type)
        {
            case TokenType.LeftParen:
                return ParseTuplePattern();

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
        var op = Current.Value;
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
            ColumnEnd = endToken.Column + endToken.Value.Length,
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
            ColumnEnd = token.Column + token.Value.Length,
            Span = GetSpanFromToken(token)
        };
    }

    private Pattern ParseIdentifierOrMemberAccessPattern()
    {
        var token = Current;
        Advance();

        if (Current.Type == TokenType.Dot)
        {
            // Parse dotted member access pattern (e.g., Color.RED)
            var parts = new List<string> { token.Value };
            Token endToken = token;

            while (Current.Type == TokenType.Dot)
            {
                Advance(); // consume '.'
                if (Current.Type != TokenType.Identifier)
                {
                    throw ReportError($"Expected identifier after '.' in pattern, got '{Current.Value}'",
                        Current.Line, Current.Column,
                        DiagnosticCodes.Parser.ExpectedPattern, span: CurrentSpan);
                }
                endToken = Current;
                parts.Add(Current.Value);
                Advance();
            }

            return new MemberAccessPattern
            {
                Parts = parts.ToImmutableArray(),
                LineStart = token.Line,
                ColumnStart = token.Column,
                LineEnd = endToken.Line,
                ColumnEnd = endToken.Column + endToken.Value.Length,
                Span = GetSpanFromTokens(token, endToken)
            };
        }

        return new BindingPattern
        {
            Name = new Identifier
            {
                Name = token.Value,
                LineStart = token.Line,
                ColumnStart = token.Column,
                LineEnd = token.Line,
                ColumnEnd = token.Column + token.Value.Length,
                Span = GetSpanFromToken(token)
            },
            LineStart = token.Line,
            ColumnStart = token.Column,
            LineEnd = token.Line,
            ColumnEnd = token.Column + token.Value.Length,
            Span = GetSpanFromToken(token)
        };
    }

    private TuplePattern ParseTuplePattern()
    {
        var startToken = Current;
        Expect(TokenType.LeftParen);

        var elements = new List<Pattern>();
        if (Current.Type != TokenType.RightParen)
        {
            elements.Add(ParsePattern());
            while (Current.Type == TokenType.Comma)
            {
                Advance();
                if (Current.Type == TokenType.RightParen)
                    break;
                elements.Add(ParsePattern());
            }
        }

        var endToken = Current;
        Expect(TokenType.RightParen);

        return new TuplePattern
        {
            Elements = elements.ToImmutableArray(),
            LineStart = startToken.Line,
            ColumnStart = startToken.Column,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Value.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
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
            ColumnEnd = endToken.Column + endToken.Value.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }
}
